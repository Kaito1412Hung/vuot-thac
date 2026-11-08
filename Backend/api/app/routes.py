import json
import uuid
from datetime import date, timedelta
from typing import Annotated, Literal

from fastapi import APIRouter, Depends, HTTPException, Query, Request, Response, status
from sqlalchemy import and_, delete, func, select
from sqlalchemy.dialects.postgresql import distinct_on
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from . import daily, rules, schemas
from .config import get_settings
from .database import get_session
from .models import Ghost, Player, Run, SaveGame
from .security import current_player, limiter, new_token, optional_player

MAX_SAVE_BYTES = 32 * 1024

router = APIRouter(prefix="/v1")
SessionDep = Annotated[Session, Depends(get_session)]
PlayerDep = Annotated[Player, Depends(current_player)]


# ---------- Người chơi ----------

@router.post("/players", response_model=schemas.PlayerCreated, status_code=status.HTTP_201_CREATED)
def create_player(body: schemas.PlayerCreate, request: Request, session: SessionDep):
    """Tạo tài khoản ẩn danh. Game lưu token này, mất token là mất tài khoản."""
    host = request.client.host if request.client else "unknown"
    limiter.check(f"players:{host}", get_settings().players_per_ip_per_hour, 3600)
    token, token_hash = new_token()
    player = Player(display_name=body.display_name, token_hash=token_hash)
    session.add(player)
    session.commit()
    return schemas.PlayerCreated(id=player.id, display_name=player.display_name, created_at=player.created_at, token=token)


@router.get("/players/me", response_model=schemas.PlayerOut)
def get_me(player: PlayerDep):
    return player


@router.patch("/players/me", response_model=schemas.PlayerOut)
def rename_me(body: schemas.PlayerUpdate, player: PlayerDep, session: SessionDep):
    player.display_name = body.display_name
    session.commit()
    return player


# ---------- Bảng xếp hạng ----------

def _board_filter(mode: str, daily_date: date | None, boat_id: str | None):
    conditions = [Run.mode == mode, Run.daily_date == daily_date if daily_date else Run.daily_date.is_(None)]
    if boat_id:
        conditions.append(Run.boat_id == boat_id)
    return and_(*conditions)


def _ranked(mode: str, daily_date: date | None, boat_id: str | None):
    """Thành tích tốt nhất của mỗi người, kèm thứ hạng. Bằng giờ thì ai đạt trước đứng trên."""
    best = (
        select(Run)
        .where(_board_filter(mode, daily_date, boat_id))
        .ext(distinct_on(Run.player_id))
        .order_by(Run.player_id, Run.time_ms, Run.created_at, Run.id)
        .subquery()
    )
    return (
        select(
            func.row_number().over(order_by=(best.c.time_ms, best.c.created_at, best.c.id)).label("rank"),
            best.c.player_id, Player.display_name, best.c.boat_id, best.c.difficulty, best.c.time_ms,
            best.c.id.label("run_id"), Ghost.id.label("ghost_id"), best.c.created_at,
        )
        .join(Player, Player.id == best.c.player_id)
        .outerjoin(Ghost, Ghost.run_id == best.c.id)
        .subquery()
    )


def _rank_of(session: Session, run: Run) -> int:
    board = _ranked(run.mode, run.daily_date, None)
    return session.scalar(select(board.c.rank).where(board.c.player_id == run.player_id)) or 0


def _entry(row) -> schemas.LeaderboardEntry:
    return schemas.LeaderboardEntry.model_validate(dict(row._mapping))


@router.get("/leaderboard", response_model=schemas.LeaderboardOut)
def leaderboard(
    session: SessionDep,
    me: Annotated[Player | None, Depends(optional_player)],
    mode: Literal["single", "cup", "daily"] = "single",
    boat_id: Annotated[str | None, Query(alias="boatId")] = None,
    daily_date: Annotated[date | None, Query(alias="date")] = None,
    limit: Annotated[int, Query(ge=1, le=100)] = 50,
    offset: Annotated[int, Query(ge=0)] = 0,
):
    if boat_id is not None and boat_id not in rules.BOAT_IDS:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_CONTENT, "Loại thuyền không tồn tại")
    daily_date = (daily_date or daily.today()) if mode == "daily" else None
    board = _ranked(mode, daily_date, boat_id)
    total = session.scalar(select(func.count()).select_from(board))
    rows = session.execute(select(board).order_by(board.c.rank).limit(limit).offset(offset)).all()
    mine = session.execute(select(board).where(board.c.player_id == me.id)).first() if me else None
    return schemas.LeaderboardOut(
        mode=mode, daily_date=daily_date, boat_id=boat_id, total=total,
        entries=[_entry(row) for row in rows], me=_entry(mine) if mine else None,
    )


# ---------- Gửi kết quả ----------

def _run_out(session: Session, run: Run, is_best: bool) -> schemas.RunOut:
    ghost_id = session.scalar(select(Ghost.id).where(Ghost.run_id == run.id))
    return schemas.RunOut(
        id=run.id, mode=run.mode, boat_id=run.boat_id, time_ms=run.time_ms, created_at=run.created_at,
        is_personal_best=is_best, ghost_id=ghost_id, rank=_rank_of(session, run),
    )


def _existing(session: Session, player_id: uuid.UUID, client_run_id: uuid.UUID) -> Run | None:
    return session.scalar(select(Run).where(Run.player_id == player_id, Run.client_run_id == client_run_id))


def _check_daily(body: schemas.RunCreate) -> None:
    if body.mode != "daily":
        if body.daily_date is not None:
            raise rules.RuleError("Chỉ thử thách ngày mới có dailyDate")
        return
    today = daily.today()
    # Cho phép gửi muộn một ngày vì game có thể lưu kết quả lúc mất mạng
    if body.daily_date is None or not today - timedelta(days=1) <= body.daily_date <= today:
        raise rules.RuleError("Ngày thử thách không hợp lệ")
    if body.layout_seed != daily.seed_for(body.daily_date):
        raise rules.RuleError("Seed không khớp thử thách của ngày")
    if body.difficulty != daily.difficulty_for(body.daily_date):
        raise rules.RuleError("Độ khó không khớp thử thách của ngày")


@router.post("/runs", response_model=schemas.RunOut, status_code=status.HTTP_201_CREATED)
def submit_run(body: schemas.RunCreate, player: PlayerDep, session: SessionDep, response: Response):
    previous = _existing(session, player.id, body.client_run_id)
    if previous:
        response.status_code = status.HTTP_200_OK
        return _run_out(session, previous, False)
    limiter.check(f"runs:{player.id}", get_settings().runs_per_player_per_minute, 60)

    try:
        rules.check_race_times(body.time_ms, body.checkpoint_times_ms)
        _check_daily(body)
        ghost_bytes = body.ghost_bytes()
        frame_count = rules.check_ghost(ghost_bytes, body.time_ms) if ghost_bytes is not None else 0
    except rules.RuleError as error:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_CONTENT, str(error)) from error

    same_key = (
        Run.player_id == player.id, Run.mode == body.mode, Run.boat_id == body.boat_id,
        Run.daily_date == body.daily_date if body.daily_date else Run.daily_date.is_(None),
    )
    old_best = session.scalar(select(func.min(Run.time_ms)).where(*same_key))
    is_best = old_best is None or body.time_ms < old_best

    run = Run(
        player_id=player.id, client_run_id=body.client_run_id, mode=body.mode, boat_id=body.boat_id,
        difficulty=body.difficulty, brain=body.brain, time_ms=body.time_ms,
        checkpoint_times_ms=body.checkpoint_times_ms, finish_rank=body.finish_rank,
        layout_seed=body.layout_seed, daily_date=body.daily_date, client_version=body.client_version,
    )
    session.add(run)
    try:
        session.flush()
    except IntegrityError:
        # Hai yêu cầu trùng mã lượt tới cùng lúc: yêu cầu kia đã ghi trước
        session.rollback()
        existing = _existing(session, player.id, body.client_run_id)
        if existing is None:
            raise
        response.status_code = status.HTTP_200_OK
        return _run_out(session, existing, False)

    if ghost_bytes is not None and is_best:
        # Mỗi người chỉ giữ ghost của lượt tốt nhất theo từng thuyền, tránh đầy ổ đĩa
        old_runs = select(Run.id).where(*same_key, Run.id != run.id)
        session.execute(delete(Ghost).where(Ghost.run_id.in_(old_runs)))
        session.add(Ghost(run_id=run.id, data=ghost_bytes, frame_count=frame_count))
    session.commit()
    return _run_out(session, run, is_best)


@router.get("/ghosts/{ghost_id}", response_class=Response, responses={200: {"content": {"application/gzip": {}}}})
def get_ghost(ghost_id: uuid.UUID, session: SessionDep):
    ghost = session.get(Ghost, ghost_id)
    if ghost is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Không có ghost này")
    return Response(ghost.data, media_type="application/gzip", headers={"Cache-Control": "public, max-age=86400, immutable"})


# ---------- Thử thách ngày ----------

@router.get("/daily-challenge", response_model=schemas.DailyChallengeOut)
def daily_challenge():
    today = daily.today()
    return schemas.DailyChallengeOut(
        date=today, seed=daily.seed_for(today), difficulty=daily.difficulty_for(today),
        track_id="suoi-rung", resets_at=daily.resets_at(today),
    )


# ---------- Tiến độ ----------

@router.get("/save", response_model=schemas.SaveOut)
def get_save(player: PlayerDep, session: SessionDep):
    save = session.get(SaveGame, player.id)
    return save if save else schemas.SaveOut(revision=0, data={}, updated_at=None)


@router.put("/save", response_model=schemas.SaveOut)
def put_save(body: schemas.SavePut, player: PlayerDep, session: SessionDep):
    """revision là số phiên bản game đang giữ. Máy khác đã lưu mới hơn thì trả 409, không ghi đè."""
    if len(json.dumps(body.data, ensure_ascii=False).encode()) > MAX_SAVE_BYTES:
        raise HTTPException(status.HTTP_413_CONTENT_TOO_LARGE, "Dữ liệu tiến độ quá lớn")
    save = session.get(SaveGame, player.id, with_for_update=True)
    current = save.revision if save else 0
    if body.revision != current:
        raise HTTPException(status.HTTP_409_CONFLICT, f"Tiến độ trên server đang ở phiên bản {current}")
    if save is None:
        save = SaveGame(player_id=player.id, revision=1, data=body.data)
        session.add(save)
    else:
        save.revision = current + 1
        save.data = body.data
    try:
        session.commit()
    except IntegrityError as error:
        # Hai máy cùng lưu lần đầu
        session.rollback()
        raise HTTPException(status.HTTP_409_CONFLICT, "Tiến độ vừa được lưu từ máy khác") from error
    session.refresh(save)
    return save
