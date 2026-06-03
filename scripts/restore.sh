#!/bin/sh
# Restore a TWC database backup.
# Make executable: chmod +x scripts/restore.sh
#
# Usage:
#   ./scripts/restore.sh <backup_file.sql.gz>
#
# WARNING: This will REPLACE the current database contents.
#
# Prerequisites:
#   - Run from the repo root with the stack (at minimum postgres) running:
#       docker compose up -d postgres
#   - POSTGRES_USER and POSTGRES_DB must be set in the shell environment,
#     or sourced from .env:  set -a && . .env && set +a
#
# Example:
#   set -a && . .env && set +a
#   ./scripts/restore.sh /path/to/twc_backup_20260601_020000.sql.gz
set -e

BACKUP_FILE="${1:?Usage: restore.sh <backup_file.sql.gz>}"

if [ ! -f "${BACKUP_FILE}" ]; then
    echo "ERROR: Backup file not found: ${BACKUP_FILE}" >&2
    exit 1
fi

echo "[$(date -u +%FT%TZ)] Restoring from ${BACKUP_FILE}..."
echo "[$(date -u +%FT%TZ)] Target: database '${POSTGRES_DB}' on the running postgres container"
echo ""
echo "Press Ctrl-C within 5 seconds to abort..."
sleep 5

gunzip -c "${BACKUP_FILE}" | docker compose exec -T postgres psql \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}"

echo ""
echo "[$(date -u +%FT%TZ)] Restore complete."
echo "Run the verification checklist in docs/BACKUP_AND_RESTORE.md before declaring success."
