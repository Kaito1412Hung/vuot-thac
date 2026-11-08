import base64
import gzip
import uuid
from datetime import date, timedelta

import pytest
from alembic import command
from alembic.config import Config
from sqlalchemy import select

from app import daily, rules
from app.database import get_engine
from app.models import Player

from .conftest import make_player, run_body, times


def ghost_b64(total_ms: int = 120_000, step: float = 1.0) -> str:
    count = round(total_ms / 100)
    frames = [(index * step, 21.0, index * step, 9000) for index in range(count)]
    return base64.b64encode(rules.encode_ghost(frames)).decode()


# ---------- Sức khoẻ và migration ----------

def test_health(client):
    assert client.get("/health").json() == {"status": "ok", "database": "ok"}


def test_models_match_migrations():
    """alembic check báo lỗi nếu models.py lệch với migration."""
    command.check(Config("alembic.ini"))


# ---------- Người chơi ----------

def test_create_player_stores_only_token_hash(client):
    player = make_player(client, "  Kaito   Hưng ")
    assert player["displayName"] == "Kaito Hưng"
    with get_engine().connect() as connection:
        stored = connection.execute(select(Player.token_hash)).scalar_one()
    assert player["token"] not in stored and len(stored) == 64


@pytest.mark.parametrize("name", ["a", "x" * 21, "ab\u0000cd", "   "])
def test_bad_names_rejected(client, name):
    assert client.post("/v1/players", json={"displayName": name}).status_code == 422


def test_auth_required_and_wrong_token_rejected(client):
    assert client.get("/v1/players/me").status_code == 401
    assert client.get("/v1/players/me", headers={"Authorization": "Bearer sai"}).status_code == 401
    assert client.post("/v1/runs", json=run_body()).status_code == 401


def test_rename(client):
    player = make_player(client)
    response = client.patch("/v1/players/me", json={"displayName": "Tên mới"}, headers=player["headers"])
    assert response.status_code == 200 and response.json()["displayName"] == "Tên mới"
    assert client.get("/v1/players/me", headers=player["headers"]).json()["displayName"] == "Tên mới"


def test_create_player_rate_limited(client, monkeypatch):
    from app.config import get_settings
    monkeypatch.setattr(get_settings(), "players_per_ip_per_hour", 2)
    make_player(client)
    make_player(client)
    response = client.post("/v1/players", json={"displayName": "Quá nhiều"})
    assert response.status_code == 429 and "retry-after" in response.headers


# ---------- Kiểm tra kết quả ----------

def test_valid_run_accepted(client):
    player = make_player(client)
    response = client.post("/v1/runs", json=run_body(), headers=player["headers"])
    assert response.status_code == 201, response.text
    body = response.json()
    assert body["rank"] == 1 and body["isPersonalBest"] is True and body["ghostId"] is None


def test_retry_same_client_run_id_counts_once(client):
    player = make_player(client)
    body = run_body()
    first = client.post("/v1/runs", json=body, headers=player["headers"])
    second = client.post("/v1/runs", json=body, headers=player["headers"])
    assert (first.status_code, second.status_code) == (201, 200)
    assert first.json()["id"] == second.json()["id"]
    assert client.get("/v1/leaderboard").json()["total"] == 1


@pytest.mark.parametrize(
    "change, message",
    [
        ({"checkpointTimesMs": times()[:-1]}, "checkpoint"),
        ({"checkpointTimesMs": [times()[1], times()[0], *times()[2:]]}, "đúng thứ tự"),
        ({"checkpointTimesMs": [-1, *times()[1:]]}, "không hợp lệ"),
        ({"timeMs": 121_000}, "trùng thời gian"),
        ({"timeMs": 430_000, "checkpointTimesMs": times(430_000)}, "giới hạn"),
        ({"timeMs": 12_000, "checkpointTimesMs": times(12_000)}, "phi lý"),
    ],
)
def test_impossible_times_rejected(client, change, message):
    player = make_player(client)
    response = client.post("/v1/runs", json=run_body() | change, headers=player["headers"])
    assert response.status_code == 422 and message in response.json()["detail"]
    assert client.get("/v1/leaderboard").json()["total"] == 0


def test_short_final_gate_is_allowed(client):
    """Cổng cuối là vạch đích nên đoạn cuối được ngắn hơn 80 m."""
    player = make_player(client)
    checkpoints = [10_000 * (index + 1) for index in range(11)] + [110_500]
    response = client.post("/v1/runs", json=run_body(110_500, checkpointTimesMs=checkpoints), headers=player["headers"])
    assert response.status_code == 201, response.text


@pytest.mark.parametrize(
    "change",
    [{"boatId": "TauNgam"}, {"difficulty": 5}, {"brain": "gpt"}, {"mode": "online"}, {"finishRank": 7}, {"khongCo": 1}],
)
def test_bad_fields_rejected(client, change):
    player = make_player(client)
    assert client.post("/v1/runs", json=run_body() | change, headers=player["headers"]).status_code == 422


def test_run_rate_limited_but_retry_is_free(client, monkeypatch):
    from app.config import get_settings
    monkeypatch.setattr(get_settings(), "runs_per_player_per_minute", 1)
    player = make_player(client)
    body = run_body()
    assert client.post("/v1/runs", json=body, headers=player["headers"]).status_code == 201
    assert client.post("/v1/runs", json=body, headers=player["headers"]).status_code == 200
    assert client.post("/v1/runs", json=run_body(), headers=player["headers"]).status_code == 429


# ---------- Bảng xếp hạng ----------

def test_leaderboard_keeps_best_per_player_and_orders(client):
    slow, fast = make_player(client, "Chậm"), make_player(client, "Nhanh")
    client.post("/v1/runs", json=run_body(150_000), headers=slow["headers"])
    client.post("/v1/runs", json=run_body(130_000), headers=slow["headers"])
    client.post("/v1/runs", json=run_body(100_000), headers=fast["headers"])
    board = client.get("/v1/leaderboard").json()
    assert board["total"] == 2
    assert [(row["rank"], row["displayName"], row["timeMs"]) for row in board["entries"]] == [(1, "Nhanh", 100_000), (2, "Chậm", 130_000)]
    assert board["me"] is None
    assert client.get("/v1/leaderboard", headers=slow["headers"]).json()["me"]["rank"] == 2


def test_leaderboard_filters_boat_mode_and_paging(client):
    player = make_player(client)
    client.post("/v1/runs", json=run_body(100_000), headers=player["headers"])
    client.post("/v1/runs", json=run_body(90_000, boatId="BeTre"), headers=player["headers"])
    client.post("/v1/runs", json=run_body(80_000, mode="cup"), headers=player["headers"])
    assert client.get("/v1/leaderboard").json()["entries"][0]["timeMs"] == 90_000
    assert client.get("/v1/leaderboard", params={"boatId": "Kayak"}).json()["entries"][0]["timeMs"] == 100_000
    assert client.get("/v1/leaderboard", params={"mode": "cup"}).json()["entries"][0]["timeMs"] == 80_000
    assert client.get("/v1/leaderboard", params={"limit": 1, "offset": 1}).json()["entries"] == []
    assert client.get("/v1/leaderboard", params={"boatId": "TauNgam"}).status_code == 422
    assert client.get("/v1/leaderboard", params={"limit": 101}).status_code == 422


# ---------- Ghost ----------

def test_ghost_round_trip_and_listed_on_leaderboard(client):
    player = make_player(client)
    encoded = ghost_b64()
    run = client.post("/v1/runs", json=run_body(ghost=encoded), headers=player["headers"]).json()
    assert run["ghostId"]
    assert client.get("/v1/leaderboard").json()["entries"][0]["ghostId"] == run["ghostId"]
    download = client.get(f"/v1/ghosts/{run['ghostId']}")
    assert download.status_code == 200
    assert download.headers["content-type"] == "application/gzip"
    assert download.content == base64.b64decode(encoded)
    assert client.get(f"/v1/ghosts/{uuid.uuid4()}").status_code == 404


def test_only_best_ghost_is_kept(client):
    player = make_player(client)
    first = client.post("/v1/runs", json=run_body(120_000, ghost=ghost_b64(120_000)), headers=player["headers"]).json()
    worse = client.post("/v1/runs", json=run_body(130_000, ghost=ghost_b64(130_000)), headers=player["headers"]).json()
    assert worse["ghostId"] is None and worse["isPersonalBest"] is False
    assert client.get(f"/v1/ghosts/{first['ghostId']}").status_code == 200
    better = client.post("/v1/runs", json=run_body(100_000, ghost=ghost_b64(100_000)), headers=player["headers"]).json()
    assert better["ghostId"] and client.get(f"/v1/ghosts/{first['ghostId']}").status_code == 404


def _gzip_b64(raw: bytes) -> str:
    return base64.b64encode(gzip.compress(raw)).decode()


@pytest.mark.parametrize(
    "ghost",
    [
        "khong-phai-base64!!",
        base64.b64encode(b"khong phai gzip").decode(),
        _gzip_b64(b"VTG1"),
        _gzip_b64(rules.GHOST_HEADER.pack(b"XXXX", 10, 0)),
        _gzip_b64(rules.GHOST_HEADER.pack(rules.GHOST_MAGIC, 30, 0)),
        _gzip_b64(rules.GHOST_HEADER.pack(rules.GHOST_MAGIC, 10, 5)),  # khai 5 khung nhưng không có dữ liệu
        ghost_b64(60_000),  # ngắn hơn nhiều so với thời gian về đích
        ghost_b64(step=50.0),  # mỗi khung nhảy quá xa
        _gzip_b64(rules.GHOST_HEADER.pack(rules.GHOST_MAGIC, 10, 1200) + rules.GHOST_FRAME.pack(float("nan"), 0, 0, 0) * 1200),
        _gzip_b64(b"\0" * (rules.MAX_GHOST_RAW_BYTES + 1)),  # bung ra quá lớn
    ],
)
def test_bad_ghost_rejected_and_run_not_saved(client, ghost):
    player = make_player(client)
    response = client.post("/v1/runs", json=run_body(ghost=ghost), headers=player["headers"])
    assert response.status_code == 422, response.text
    assert client.get("/v1/leaderboard").json()["total"] == 0


def test_a_few_respawn_jumps_are_allowed(client):
    player = make_player(client)
    frames = [(float(index % 400), 21.0, float(index // 400) * 200, 0) for index in range(1200)]
    encoded = base64.b64encode(rules.encode_ghost(frames)).decode()
    assert client.post("/v1/runs", json=run_body(ghost=encoded), headers=player["headers"]).status_code == 201


# ---------- Thử thách ngày ----------

def test_daily_is_stable_and_valid(client):
    first, second = client.get("/v1/daily-challenge").json(), client.get("/v1/daily-challenge").json()
    assert first == second
    assert 0 <= first["seed"] < 2**31 and 0 <= first["difficulty"] <= 4 and first["trackId"] == "suoi-rung"
    assert first["date"] == daily.today().isoformat()
    assert daily.seed_for(date(2026, 1, 1)) != daily.seed_for(date(2026, 1, 2))


def _daily_run(day: date, total_ms: int = 120_000, **overrides) -> dict:
    return run_body(total_ms, mode="daily", dailyDate=day.isoformat(), layoutSeed=daily.seed_for(day), difficulty=daily.difficulty_for(day)) | overrides


def test_daily_run_and_board_are_per_day(client):
    player = make_player(client)
    today = daily.today()
    assert client.post("/v1/runs", json=_daily_run(today), headers=player["headers"]).status_code == 201
    assert client.post("/v1/runs", json=_daily_run(today - timedelta(days=1), 110_000), headers=player["headers"]).status_code == 201
    board = client.get("/v1/leaderboard", params={"mode": "daily"}).json()
    assert board["dailyDate"] == today.isoformat() and board["total"] == 1 and board["entries"][0]["timeMs"] == 120_000
    yesterday = client.get("/v1/leaderboard", params={"mode": "daily", "date": (today - timedelta(days=1)).isoformat()}).json()
    assert yesterday["entries"][0]["timeMs"] == 110_000
    assert client.get("/v1/leaderboard").json()["total"] == 0  # không lẫn sang bảng thường


def test_daily_rejects_wrong_seed_difficulty_or_date(client):
    player = make_player(client)
    today = daily.today()
    seed = daily.seed_for(today)
    bad = [
        _daily_run(today, layoutSeed=(seed + 1) % 2**31),
        _daily_run(today, difficulty=(daily.difficulty_for(today) + 1) % 5),
        _daily_run(today - timedelta(days=2)),
        _daily_run(today + timedelta(days=1)),
        run_body(mode="daily"),
        run_body(dailyDate=today.isoformat()),
    ]
    for body in bad:
        assert client.post("/v1/runs", json=body, headers=player["headers"]).status_code == 422


# ---------- Tiến độ ----------

def test_save_revision_flow(client):
    player = make_player(client)
    headers = player["headers"]
    assert client.get("/v1/save", headers=headers).json() == {"revision": 0, "data": {}, "updatedAt": None}
    first = client.put("/v1/save", json={"revision": 0, "data": {"bestTime": 120.5}}, headers=headers)
    assert first.status_code == 200 and first.json()["revision"] == 1
    # Máy thứ hai vẫn giữ phiên bản cũ thì không được ghi đè
    conflict = client.put("/v1/save", json={"revision": 0, "data": {"bestTime": 999}}, headers=headers)
    assert conflict.status_code == 409
    second = client.put("/v1/save", json={"revision": 1, "data": {"bestTime": 100}}, headers=headers)
    assert second.json()["revision"] == 2
    assert client.get("/v1/save", headers=headers).json()["data"] == {"bestTime": 100}


def test_save_is_private_and_size_limited(client):
    alice, bob = make_player(client, "Alice"), make_player(client, "Bob")
    client.put("/v1/save", json={"revision": 0, "data": {"cua": "Alice"}}, headers=alice["headers"])
    assert client.get("/v1/save", headers=bob["headers"]).json()["data"] == {}
    assert client.get("/v1/save").status_code == 401
    large = {"x": "a" * (33 * 1024)}
    assert client.put("/v1/save", json={"revision": 0, "data": large}, headers=bob["headers"]).status_code == 413
