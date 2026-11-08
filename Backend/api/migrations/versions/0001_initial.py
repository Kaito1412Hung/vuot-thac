"""Bảng người chơi, kết quả đua, ghost và tiến độ.

Revision ID: 0001
"""

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision = "0001"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "players",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("display_name", sa.String(20), nullable=False),
        sa.Column("token_hash", sa.String(64), nullable=False, unique=True),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
    )
    op.create_table(
        "runs",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("player_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("players.id", ondelete="CASCADE"), nullable=False),
        sa.Column("client_run_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("mode", sa.String(10), nullable=False),
        sa.Column("boat_id", sa.String(20), nullable=False),
        sa.Column("difficulty", sa.SmallInteger(), nullable=False),
        sa.Column("brain", sa.String(10), nullable=False),
        sa.Column("time_ms", sa.Integer(), nullable=False),
        sa.Column("checkpoint_times_ms", postgresql.ARRAY(sa.Integer()), nullable=False),
        sa.Column("finish_rank", sa.SmallInteger(), nullable=True),
        sa.Column("layout_seed", sa.Integer(), nullable=True),
        sa.Column("daily_date", sa.Date(), nullable=True),
        sa.Column("client_version", sa.String(20), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.UniqueConstraint("player_id", "client_run_id", name="uq_runs_player_client_run"),
        sa.CheckConstraint("time_ms > 0", name="ck_runs_time_positive"),
        sa.CheckConstraint("difficulty BETWEEN 0 AND 4", name="ck_runs_difficulty"),
        sa.CheckConstraint("(mode = 'daily') = (daily_date IS NOT NULL)", name="ck_runs_daily_date"),
    )
    op.create_index("ix_runs_board", "runs", ["mode", "daily_date", "time_ms"])
    op.create_index("ix_runs_player_board", "runs", ["player_id", "mode", "daily_date", "boat_id", "time_ms"])
    op.create_table(
        "ghosts",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("run_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("runs.id", ondelete="CASCADE"), nullable=False, unique=True),
        sa.Column("data", sa.LargeBinary(), nullable=False),
        sa.Column("frame_count", sa.Integer(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
    )
    op.create_table(
        "saves",
        sa.Column("player_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("players.id", ondelete="CASCADE"), primary_key=True),
        sa.Column("revision", sa.Integer(), nullable=False),
        sa.Column("data", postgresql.JSONB(), nullable=False),
        sa.Column("updated_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
    )


def downgrade() -> None:
    op.drop_table("saves")
    op.drop_table("ghosts")
    op.drop_table("runs")
    op.drop_table("players")
