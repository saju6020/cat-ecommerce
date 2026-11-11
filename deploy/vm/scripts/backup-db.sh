#!/usr/bin/env bash
# deploy/vm -- nightly database dump (interim, until the pgBackRest setup in deploy/backup gets its off-site repository).
# Keeps 7 days on the VM disk and copies each dump to the private backup bucket. usage (the deploy user's crontab; the VM
# clock is Asia/Dhaka, so this is 01:00 Dhaka time; the user must be in the docker group):
#   0 1 * * * /opt/purrcare/src/deploy/vm/scripts/backup-db.sh >> /var/log/purrcare-backup.log 2>&1
set -euo pipefail
umask 077
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
env_file="${PURRCARE_ENV:-/opt/purrcare/.env}"
bucket="$(grep '^BACKUP_BUCKET=' "$env_file" | cut -d= -f2-)"
dir=/opt/purrcare/backups
mkdir -p "$dir"
file="$dir/catecommerce-$(date -u +%Y%m%dT%H%M%SZ).dump"

"$here/compose.sh" exec -T postgres pg_dump -U postgres -d catecommerce -Fc > "$file.part"
mv "$file.part" "$file"
gcloud storage cp --quiet "$file" "gs://$bucket/db/"
find "$dir" -name 'catecommerce-*.dump' -mtime +7 -delete
echo "$(date -u +%FT%TZ) backup ok: $(basename "$file") $(stat -c %s "$file") bytes"
