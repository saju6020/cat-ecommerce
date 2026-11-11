#!/usr/bin/env bash
# deploy/vm -- docker compose with the VM's .env and this repository's compose file. usage: compose.sh <compose args...>
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec docker compose --env-file "${PURRCARE_ENV:-/opt/purrcare/.env}" -f "$here/../docker-compose.yml" "$@"
