import os
import uuid

import pytest
from alembic import command
from alembic.config import Config
from fastapi.testclient import TestClient
from sqlalchemy import create_engine, text
from sqlalchemy.engine import make_url

from app.database import get_engine
from app.main import app
from app.models import Base
from app.security import limiter


@pytest.fixture(scope="session", autouse=True)
def database():
    """Tạo database test mới rồi dựng bảng bằng chính migration, giống môi trường thật."""
    name = make_url(os.environ["DATABASE_URL"]).database
    assert name.endswith("_test"), "Test xoá dữ liệu nên chỉ được chạy trên database có đuôi _test"
    admin = create_engine(os.environ["ADMIN_DATABASE_URL"], isolation_level="AUTOCOMMIT")
    with admin.connect() as connection:
        connection.execute(text(f'DROP DATABASE IF EXISTS "{name}" WITH (FORCE)'))
        connection.execute(text(f'CREATE DATABASE "{name}"'))
    command.upgrade(Config("alembic.ini"), "head")
    yield
    get_engine().dispose()
    with admin.connect() as connection:
        connection.execute(text(f'DROP DATABASE IF EXISTS "{name}" WITH (FORCE)'))
    admin.dispose()


@pytest.fixture(autouse=True)
def clean():
    with get_engine().begin() as connection:
        connection.execute(text("TRUNCATE " + ", ".join(table.name for table in Base.metadata.sorted_tables) + " CASCADE"))
    limiter.reset()


@pytest.fixture
def client():
    with TestClient(app) as test_client:
        yield test_client


def make_player(client: TestClient, name: str = "Người chơi") -> dict:
    response = client.post("/v1/players", json={"displayName": name})
    assert response.status_code == 201, response.text
    body = response.json()
    body["headers"] = {"Authorization": f"Bearer {body['token']}"}
    return body


def times(total_ms: int = 120_000) -> list[int]:
    """12 mốc đều nhau, mốc cuối đúng bằng thời gian về đích."""
    return [round(total_ms * (index + 1) / 12) for index in range(12)]


def run_body(total_ms: int = 120_000, **overrides) -> dict:
    body = {
        "clientRunId": str(uuid.uuid4()),
        "mode": "single",
        "boatId": "Kayak",
        "difficulty": 2,
        "brain": "ppo",
        "timeMs": total_ms,
        "checkpointTimesMs": times(total_ms),
        "finishRank": 1,
        "layoutSeed": 12345,
        "clientVersion": "0.5.0",
    }
    body.update(overrides)
    return body
