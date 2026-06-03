#!/bin/sh
# Nightly PostgreSQL backup script for TWC.
# Runs inside the backup sidecar container (postgres:16-alpine image).
# Make executable: chmod +x scripts/backup.sh
#
# Environment variables required (injected by docker-compose.yml):
#   POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
#
# Volume mount expected:
#   /backups  — the twc_postgres_backups named volume
set -e

BACKUP_DIR=/backups
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/twc_backup_${TIMESTAMP}.sql.gz"

echo "[$(date -u +%FT%TZ)] Starting backup to ${BACKUP_FILE}"

PGPASSWORD="${POSTGRES_PASSWORD}" pg_dump \
    -h postgres \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}" \
    --no-owner \
    --no-privileges \
    | gzip > "${BACKUP_FILE}"

echo "[$(date -u +%FT%TZ)] Backup complete: ${BACKUP_FILE}"

# Rotate: delete dumps older than 14 days
find "${BACKUP_DIR}" -name "twc_backup_*.sql.gz" -mtime +14 -delete
echo "[$(date -u +%FT%TZ)] Rotation complete"
