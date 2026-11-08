FROM python:3.12-slim AS base
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /srv
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY alembic.ini .
COPY migrations migrations
COPY app app
RUN useradd --create-home --uid 10001 api
USER api

FROM base AS production
EXPOSE 8000
# Chạy một tiến trình: bộ giới hạn tần suất nằm trong bộ nhớ của tiến trình này.
# Chỉ Caddy gọi được cổng 8000 nên tin header X-Forwarded-For từ Caddy.
CMD ["sh", "-c", "alembic upgrade head && exec uvicorn app.main:app --host 0.0.0.0 --port 8000 --proxy-headers --forwarded-allow-ips=*"]

FROM base AS test
USER root
COPY requirements-dev.txt .
RUN pip install --no-cache-dir -r requirements-dev.txt
COPY tests tests
USER api
CMD ["pytest", "-q", "-p", "no:cacheprovider"]
