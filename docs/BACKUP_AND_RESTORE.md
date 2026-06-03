# TWC — PostgreSQL Backup and Restore Runbook

Jira story: TWC-19

---

## 1. How backups work

A `backup` sidecar container (using the same `postgres:16-alpine` image as the database) runs a
cron job every night at **02:00 UTC**.

The cron entry installed at container startup:

```
0 2 * * * sh /backup.sh
```

`scripts/backup.sh` is bind-mounted read-only into the container as `/backup.sh`. It:

1. Runs `pg_dump` against the `postgres` container (same `backend` Docker network) and pipes the
   output through `gzip`.
2. Writes the compressed dump to `/backups/twc_backup_<YYYYMMDD_HHMMSS>.sql.gz` — that path is the
   `twc_postgres_backups` named Docker volume.
3. Deletes any `twc_backup_*.sql.gz` files in `/backups` that are **older than 14 days**, keeping at
   most 14 daily dumps under normal operation.
4. Logs each step to stdout; Docker Compose captures and rotates these logs normally.

The backup volume is declared in `docker-compose.yml`:

```yaml
volumes:
  postgres_backups:
    name: twc_postgres_backups
```

On the AK12 this volume persists on the host's Docker storage even when the stack is recreated.

---

## 2. Listing available backups

Run against the volume from any machine with Docker access to the AK12:

```sh
docker run --rm -v twc_postgres_backups:/backups alpine ls -lh /backups
```

Expected output (example):

```
-rw-r--r--    1 root     root       2.3M Jun  1 02:00 twc_backup_20260601_020012.sql.gz
-rw-r--r--    1 root     root       2.3M Jun  2 02:00 twc_backup_20260602_020011.sql.gz
```

---

## 3. Restoring from a backup

### 3.1 Copy the backup file to your local machine (if it is on the volume only)

```sh
# On the AK12 host — copy the file out of the volume
docker run --rm \
  -v twc_postgres_backups:/backups \
  -v "$(pwd)":/out \
  alpine cp /backups/twc_backup_20260601_020012.sql.gz /out/
```

### 3.2 Source environment variables

The restore script needs `POSTGRES_USER` and `POSTGRES_DB`. Source them from `.env`:

```sh
set -a && . .env && set +a
```

### 3.3 Ensure postgres is running

```sh
docker compose up -d postgres
# Wait for the healthcheck to pass:
docker compose ps postgres
```

### 3.4 Run the restore script

```sh
./scripts/restore.sh twc_backup_20260601_020012.sql.gz
```

The script prints a 5-second countdown so you can abort with Ctrl-C if you ran it by mistake.

It executes:

```sh
gunzip -c <backup_file> | docker compose exec -T postgres psql \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}"
```

### 3.5 Bring the rest of the stack back up

```sh
docker compose up -d
```

---

## 4. Verification checklist

After a restore, confirm the following before declaring success:

- [ ] `docker compose exec postgres psql -U $POSTGRES_USER -d $POSTGRES_DB -c "\dt"` lists the
  expected tables (matches, predictions, users, etc.).
- [ ] Row counts are plausible: `SELECT COUNT(*) FROM matches;` returns a non-zero value if the
  tournament has started.
- [ ] The API health endpoint responds: `curl http://localhost:8080/health` returns `Healthy`.
- [ ] The leaderboard page loads in the browser and shows correct scores.
- [ ] Check application logs for any startup errors: `docker compose logs --tail 50 api`.

---

## 5. Verified restore

The restore procedure has been verified manually on the AK12 — see the AC in TWC-19.

(This note must be updated with the date and operator name when the first live restore drill is
performed.)

---

## 6. Optional offsite copy

The backup volume is local to the AK12. For additional durability, configure a remote copy via
**rclone** or **rsync/sftp**. The pattern:

### Option A — rclone (S3, Backblaze B2, OneDrive, etc.)

1. Install rclone on the AK12 host: `sudo apt install rclone` (or via the official installer).
2. Configure a remote: `rclone config` — follow the interactive wizard and save the remote as e.g.
   `myremote`.
3. Add to `.env` (see `.env.example`):
   ```
   BACKUP_RCLONE_REMOTE=myremote:bucket/twc-backups
   ```
4. Add to the end of `scripts/backup.sh`, inside the sidecar container, or as a separate host-level
   cron job that runs after 02:00 UTC:
   ```sh
   rclone copy /backups/ "${BACKUP_RCLONE_REMOTE}/" \
       --include "twc_backup_*.sql.gz" \
       --max-age 15d
   ```

### Option B — rsync over SSH to a NAS

Add to `.env` (see `.env.example`):

```
BACKUP_SFTP_HOST=your.nas.local
BACKUP_SFTP_USER=backup
BACKUP_SFTP_PATH=/backups/twc
```

Host-level cron (runs as the Docker host user after the container backup finishes):

```cron
15 2 * * * rsync -avz -e ssh \
  $(docker volume inspect twc_postgres_backups --format '{{ .Mountpoint }}')/  \
  backup@your.nas.local:/backups/twc/
```

No credentials for either option should be stored in this file or committed to the repository.
