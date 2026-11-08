#!/usr/bin/env bash
# Dựng Backend trên VPS Ubuntu. Chạy lại được để cập nhật; file .env đã có thì giữ nguyên.
# Cách dùng (quyền root): bash setup.sh api.ten-mien.com
set -euo pipefail

DOMAIN="${1:?Cần tên miền, ví dụ: bash setup.sh api.ten-mien.com}"
BACKEND_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BACKUP_DIR=/opt/vuotthac/backups
cd "$BACKEND_DIR"

if ! command -v docker >/dev/null; then
  curl -fsSL https://get.docker.com | sh
fi

# Chỉ mở SSH và web. Database và API không ra ngoài được.
apt-get update -qq && apt-get install -y -qq ufw
ufw allow 22/tcp && ufw allow 80/tcp && ufw allow 443/tcp
ufw --force enable

if [ ! -f .env ]; then
  umask 077
  cat > .env <<EOF
POSTGRES_DB=vuotthac
POSTGRES_USER=vuotthac
POSTGRES_PASSWORD=$(openssl rand -hex 24)
DAILY_SECRET=$(openssl rand -hex 32)
SITE_ADDRESS=$DOMAIN
HTTP_PORT=80
HTTPS_PORT=443
ENABLE_DOCS=false
EOF
fi

docker compose up -d --build

# Sao lưu database 3 giờ sáng mỗi ngày, giữ 14 bản gần nhất
mkdir -p "$BACKUP_DIR"
cat > /etc/cron.d/vuotthac-backup <<EOF
0 3 * * * root cd $BACKEND_DIR && docker compose exec -T db sh -c 'pg_dump -U "\$POSTGRES_USER" "\$POSTGRES_DB"' | gzip > $BACKUP_DIR/db-\$(date +\%F).sql.gz && find $BACKUP_DIR -name 'db-*.sql.gz' -mtime +14 -delete
EOF

echo "Xong. Kiểm tra: curl https://$DOMAIN/health"
