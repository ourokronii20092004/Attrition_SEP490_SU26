#!/bin/sh
# Attrition Postgres backup loop.
#
# Runs inside a small container (postgres:16-alpine, same major version as the server so
# pg_dump is compatible). On a fixed interval it takes a compressed custom-format dump of
# the whole database, writes it to /backups, prunes old dumps, and optionally ships a copy
# off-host via rclone.
#
# WHY custom format (-Fc): compressed, and restorable selectively with pg_restore.
# WHY this lives in /backups bind-mounted to a host path OUTSIDE the project dir: deploy.py
#   wipes the project dir on every deploy; backups must survive that. See docs/backups.md.
set -eu

: "${POSTGRES_HOST:=postgres}"
: "${POSTGRES_USER:=postgres}"
: "${POSTGRES_DB:=attrition}"
: "${BACKUP_INTERVAL_HOURS:=24}"   # how often to dump
: "${BACKUP_KEEP:=7}"              # how many dumps to retain
: "${BACKUP_DIR:=/backups}"
# Optional off-host shipping: set BACKUP_RCLONE_REMOTE (e.g. "b2:attrition-backups") and
# mount an rclone.conf; if unset, backups stay local (still survive deploys, but same host).
: "${BACKUP_RCLONE_REMOTE:=}"

export PGPASSWORD="${POSTGRES_PASSWORD:-postgres}"

log() { echo "[db-backup] $(date -u '+%Y-%m-%dT%H:%M:%SZ') $*"; }

backup_once() {
    mkdir -p "$BACKUP_DIR"
    ts=$(date -u '+%Y%m%d-%H%M%S')
    out="$BACKUP_DIR/attrition-$ts.dump"
    log "dumping $POSTGRES_DB -> $out"
    if pg_dump -h "$POSTGRES_HOST" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f "$out.tmp"; then
        mv "$out.tmp" "$out"
        log "dump ok ($(du -h "$out" | cut -f1))"
    else
        log "ERROR: pg_dump failed; leaving previous backups intact"
        rm -f "$out.tmp"
        return 1
    fi

    # Prune: keep the newest BACKUP_KEEP dumps, delete the rest.
    count=$(ls -1 "$BACKUP_DIR"/attrition-*.dump 2>/dev/null | wc -l)
    if [ "$count" -gt "$BACKUP_KEEP" ]; then
        ls -1t "$BACKUP_DIR"/attrition-*.dump | tail -n +"$((BACKUP_KEEP + 1))" | while read -r old; do
            log "pruning old backup $old"
            rm -f "$old"
        done
    fi

    # Optional off-host copy. rclone must be present and configured (mounted rclone.conf).
    if [ -n "$BACKUP_RCLONE_REMOTE" ] && command -v rclone >/dev/null 2>&1; then
        log "shipping $out -> $BACKUP_RCLONE_REMOTE"
        rclone copy "$out" "$BACKUP_RCLONE_REMOTE" || log "WARN: rclone copy failed (kept local copy)"
    fi
}

log "starting; interval=${BACKUP_INTERVAL_HOURS}h keep=${BACKUP_KEEP} dir=${BACKUP_DIR}"
while true; do
    backup_once || log "backup cycle failed; will retry next interval"
    sleep "$((BACKUP_INTERVAL_HOURS * 3600))"
done
