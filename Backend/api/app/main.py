from fastapi import Depends, FastAPI, Request, Response, status
from fastapi.responses import JSONResponse
from sqlalchemy import text
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.orm import Session

from .config import get_settings
from .database import get_session
from .routes import router

MAX_BODY_BYTES = 1024 * 1024

settings = get_settings()
app = FastAPI(
    title="Vượt Thác API",
    version="1.0.0",
    docs_url="/docs" if settings.enable_docs else None,
    redoc_url=None,
    openapi_url="/openapi.json" if settings.enable_docs else None,
)


@app.middleware("http")
async def limit_body(request: Request, call_next):
    # Caddy cũng chặn ở 1 MB; đây là lớp thứ hai khi gọi thẳng API
    length = request.headers.get("content-length")
    if length and length.isdigit() and int(length) > MAX_BODY_BYTES:
        return JSONResponse({"detail": "Dữ liệu gửi lên quá lớn"}, status.HTTP_413_CONTENT_TOO_LARGE)
    return await call_next(request)


@app.get("/health")
def health(response: Response, session: Session = Depends(get_session)):
    try:
        session.execute(text("SELECT 1"))
    except SQLAlchemyError:
        response.status_code = status.HTTP_503_SERVICE_UNAVAILABLE
        return {"status": "error", "database": "unreachable"}
    return {"status": "ok", "database": "ok"}


app.include_router(router)
