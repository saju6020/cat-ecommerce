#!/usr/bin/env bash
# deploy/vm -- builds the three images on the VM. The source is in /opt/purrcare/src; the API's build output arrives as
# /opt/purrcare/api-build.tar.gz from publish-api.sh (see that script for why it is built elsewhere). usage: build.sh
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
api_build="${PURRCARE_API_BUILD:-/opt/purrcare/api-build.tar.gz}"
site="$(grep '^SITE_HOST=' "${PURRCARE_ENV:-/opt/purrcare/.env}" | cut -d= -f2-)"
[ -n "$site" ] || { echo "SITE_HOST missing from .env" >&2; exit 1; }
[ -f "$api_build" ] || { echo "$api_build missing: run publish-api.sh on a developer machine and copy it here" >&2; exit 1; }
rm -rf "$here/../out" && mkdir -p "$here/../out" && tar -xzf "$api_build" -C "$here/../out"
cd "$root"
docker build -f deploy/vm/api.Dockerfile --target api -t purrcare-api:latest deploy/vm
docker build -f deploy/vm/api.Dockerfile --target migrator -t purrcare-migrator:latest deploy/vm
docker build -f deploy/vm/web.Dockerfile --build-arg "NEXT_PUBLIC_API_BASE_URL=https://$site" -t purrcare-web:latest .
