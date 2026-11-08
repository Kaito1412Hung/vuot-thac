import hashlib
import hmac
from datetime import date, datetime, time, timedelta, timezone

from .config import get_settings


def _zone() -> timezone:
    return timezone(timedelta(hours=get_settings().daily_utc_offset_hours))


def today() -> date:
    return datetime.now(_zone()).date()


def resets_at(day: date) -> datetime:
    return datetime.combine(day + timedelta(days=1), time.min, _zone())


def _digest(day: date) -> bytes:
    return hmac.new(get_settings().daily_secret.encode(), day.isoformat().encode(), hashlib.sha256).digest()


def seed_for(day: date) -> int:
    """Seed số nguyên dương 31 bit: mọi người chơi cùng một bố cục trong ngày."""
    return int.from_bytes(_digest(day)[:4], "big") & 0x7FFFFFFF


def difficulty_for(day: date) -> int:
    return _digest(day)[4] % 5
