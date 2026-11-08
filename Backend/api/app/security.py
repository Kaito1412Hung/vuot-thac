import hashlib
import secrets
import threading
import time
from collections import defaultdict, deque
from typing import Annotated

from fastapi import Depends, HTTPException, Request, status
from sqlalchemy import select
from sqlalchemy.orm import Session

from .database import get_session
from .models import Player


def hash_token(token: str) -> str:
    return hashlib.sha256(token.encode()).hexdigest()


def new_token() -> tuple[str, str]:
    token = secrets.token_urlsafe(32)
    return token, hash_token(token)


def _bearer(request: Request) -> str | None:
    scheme, _, value = request.headers.get("authorization", "").partition(" ")
    return value.strip() if scheme.lower() == "bearer" and value.strip() else None


def optional_player(request: Request, session: Annotated[Session, Depends(get_session)]) -> Player | None:
    token = _bearer(request)
    return session.scalar(select(Player).where(Player.token_hash == hash_token(token))) if token else None


def current_player(player: Annotated[Player | None, Depends(optional_player)]) -> Player:
    if player is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Thiếu hoặc sai token", {"WWW-Authenticate": "Bearer"})
    return player


class RateLimiter:
    """Giới hạn số lần trong một khoảng thời gian, lưu trong bộ nhớ tiến trình API."""

    def __init__(self) -> None:
        self._hits: dict[str, deque[float]] = defaultdict(deque)
        self._lock = threading.Lock()

    def check(self, key: str, limit: int, window_seconds: float) -> None:
        now = time.monotonic()
        with self._lock:
            hits = self._hits[key]
            while hits and now - hits[0] > window_seconds:
                hits.popleft()
            if len(hits) >= limit:
                retry = int(window_seconds - (now - hits[0])) + 1
                raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "Gửi quá nhanh, thử lại sau", {"Retry-After": str(retry)})
            hits.append(now)

    def reset(self) -> None:
        with self._lock:
            self._hits.clear()


limiter = RateLimiter()
