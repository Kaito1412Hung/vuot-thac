import base64
import binascii
import unicodedata
import uuid
from datetime import date, datetime
from typing import Annotated, Literal

from pydantic import AfterValidator, BaseModel, ConfigDict, Field, field_validator
from pydantic.alias_generators import to_camel

from . import rules


class Schema(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, from_attributes=True, extra="forbid")


def _clean_name(value: str) -> str:
    name = " ".join(unicodedata.normalize("NFC", value).split())
    if any(unicodedata.category(character).startswith("C") for character in name):
        raise ValueError("Tên có ký tự không hợp lệ")
    if not 2 <= len(name) <= 20:
        raise ValueError("Tên cần từ 2 đến 20 ký tự")
    return name


DisplayName = Annotated[str, AfterValidator(_clean_name)]


class PlayerCreate(Schema):
    display_name: DisplayName


class PlayerUpdate(Schema):
    display_name: DisplayName


class PlayerOut(Schema):
    id: uuid.UUID
    display_name: str
    created_at: datetime


class PlayerCreated(PlayerOut):
    token: str


class RunCreate(Schema):
    client_run_id: uuid.UUID
    mode: Literal["single", "cup", "daily"]
    boat_id: str
    difficulty: int = Field(ge=0, le=4)
    brain: Literal["ppo", "fsm"]
    time_ms: int = Field(gt=0)
    checkpoint_times_ms: list[int] = Field(max_length=rules.CHECKPOINT_COUNT)
    finish_rank: int | None = Field(default=None, ge=1, le=6)
    layout_seed: int | None = Field(default=None, ge=-(2**31), le=2**31 - 1)
    daily_date: date | None = None
    client_version: str = Field(min_length=1, max_length=20)
    ghost: str | None = Field(default=None, max_length=(rules.MAX_GHOST_GZIP_BYTES * 4) // 3 + 4)

    @field_validator("boat_id")
    @classmethod
    def _known_boat(cls, value: str) -> str:
        if value not in rules.BOAT_IDS:
            raise ValueError("Loại thuyền không tồn tại")
        return value

    def ghost_bytes(self) -> bytes | None:
        if self.ghost is None:
            return None
        try:
            return base64.b64decode(self.ghost, validate=True)
        except binascii.Error as error:
            raise rules.RuleError("Ghost không phải base64 hợp lệ") from error


class RunOut(Schema):
    id: uuid.UUID
    mode: str
    boat_id: str
    time_ms: int
    created_at: datetime
    is_personal_best: bool
    ghost_id: uuid.UUID | None
    rank: int


class LeaderboardEntry(Schema):
    rank: int
    player_id: uuid.UUID
    display_name: str
    boat_id: str
    difficulty: int
    time_ms: int
    run_id: uuid.UUID
    ghost_id: uuid.UUID | None
    created_at: datetime


class LeaderboardOut(Schema):
    mode: str
    daily_date: date | None
    boat_id: str | None
    total: int
    entries: list[LeaderboardEntry]
    me: LeaderboardEntry | None


class SavePut(Schema):
    revision: int = Field(ge=0)
    data: dict


class SaveOut(Schema):
    revision: int
    data: dict
    updated_at: datetime | None


class DailyChallengeOut(Schema):
    date: date
    seed: int
    difficulty: int
    track_id: str
    resets_at: datetime
