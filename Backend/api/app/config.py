from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Cấu hình đọc từ biến môi trường. Bí mật không có giá trị mặc định."""

    model_config = SettingsConfigDict(extra="ignore")

    database_url: str
    daily_secret: str
    enable_docs: bool = False
    daily_utc_offset_hours: int = 7  # Thử thách ngày đổi lúc 0 giờ giờ Việt Nam
    players_per_ip_per_hour: int = 20
    runs_per_player_per_minute: int = 30


@lru_cache
def get_settings() -> Settings:
    return Settings()
