#!/usr/bin/env bash
# deploy/vm -- writes /opt/purrcare/.env once, with freshly generated secrets. Never overwrites an existing file.
# usage: make-env.sh <site-host> <files-bucket> <backup-bucket>
set -euo pipefail
umask 077
target="${PURRCARE_ENV:-/opt/purrcare/.env}"
[ -e "$target" ] && { echo "$target already exists; not touching it" >&2; exit 1; }
site="${1:?site host, e.g. 34-12-56-78.sslip.io}"
bucket="${2:?files bucket}"
backups="${3:?backup bucket}"
secret() { openssl rand -base64 48 | tr -d '/+=\n' | cut -c1-40; }
cat > "$target" <<EOF
SITE_HOST=$site
GCS_BUCKET=$bucket
BACKUP_BUCKET=$backups
POSTGRES_SUPERUSER_PASSWORD=$(secret)
MIGRATOR_DB_PASSWORD=$(secret)
APP_DB_PASSWORD=$(secret)
REPORTER_DB_PASSWORD=$(secret)
FILE_URL_SIGNING_KEY=$(secret)
EOF
chmod 600 "$target"
echo "wrote $target (secrets generated, not printed)"
