import uuid
from datetime import date, datetime

from sqlalchemy import CheckConstraint, Date, DateTime, ForeignKey, Index, Integer, LargeBinary, SmallInteger, String, UniqueConstraint, func
from sqlalchemy.dialects.postgresql import ARRAY, JSONB, UUID
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


class Base(DeclarativeBase):
    pass


class Player(Base):
    __tablename__ = "players"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    display_name: Mapped[str] = mapped_column(String(20))
    # Chỉ lưu SHA-256 của token. Lộ database cũng không dùng được để đăng nhập.
    token_hash: Mapped[str] = mapped_column(String(64), unique=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


class Run(Base):
    __tablename__ = "runs"
    __table_args__ = (
        UniqueConstraint("player_id", "client_run_id", name="uq_runs_player_client_run"),
        CheckConstraint("time_ms > 0", name="ck_runs_time_positive"),
        CheckConstraint("difficulty BETWEEN 0 AND 4", name="ck_runs_difficulty"),
        CheckConstraint("(mode = 'daily') = (daily_date IS NOT NULL)", name="ck_runs_daily_date"),
        Index("ix_runs_board", "mode", "daily_date", "time_ms"),
        Index("ix_runs_player_board", "player_id", "mode", "daily_date", "boat_id", "time_ms"),
    )

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    player_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("players.id", ondelete="CASCADE"))
    # Mã do game tạo cho mỗi lượt: gửi lại khi mạng lỗi cũng không bị tính hai lần.
    client_run_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True))
    mode: Mapped[str] = mapped_column(String(10))
    boat_id: Mapped[str] = mapped_column(String(20))
    difficulty: Mapped[int] = mapped_column(SmallInteger)
    brain: Mapped[str] = mapped_column(String(10))
    time_ms: Mapped[int] = mapped_column(Integer)
    checkpoint_times_ms: Mapped[list[int]] = mapped_column(ARRAY(Integer))
    finish_rank: Mapped[int | None] = mapped_column(SmallInteger)
    layout_seed: Mapped[int | None] = mapped_column(Integer)
    daily_date: Mapped[date | None] = mapped_column(Date)
    client_version: Mapped[str] = mapped_column(String(20))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


class Ghost(Base):
    __tablename__ = "ghosts"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    run_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("runs.id", ondelete="CASCADE"), unique=True)
    data: Mapped[bytes] = mapped_column(LargeBinary)
    frame_count: Mapped[int] = mapped_column(Integer)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


class SaveGame(Base):
    __tablename__ = "saves"

    player_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("players.id", ondelete="CASCADE"), primary_key=True)
    revision: Mapped[int] = mapped_column(Integer)
    data: Mapped[dict] = mapped_column(JSONB)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())
