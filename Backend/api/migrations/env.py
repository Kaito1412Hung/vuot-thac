from alembic import context

from app.database import get_engine
from app.models import Base

target_metadata = Base.metadata


def run() -> None:
    # Dùng sẵn kết nối khi test truyền vào, nếu không thì nối theo DATABASE_URL
    connection = context.config.attributes.get("connection")
    if connection is not None:
        context.configure(connection=connection, target_metadata=target_metadata, compare_type=True)
        with context.begin_transaction():
            context.run_migrations()
        return
    with get_engine().connect() as connection:
        context.configure(connection=connection, target_metadata=target_metadata, compare_type=True)
        with context.begin_transaction():
            context.run_migrations()


run()
