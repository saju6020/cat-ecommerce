#!/usr/bin/env bash
# deploy/vm -- applies EF migrations as catecommerce_migrator, then re-runs roles.sql (idempotent) so new objects are adopted
# and granted. Run after every build that may carry a migration, before restarting the API. usage: migrate.sh
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
env_file="${PURRCARE_ENV:-/opt/purrcare/.env}"
get() { grep "^$1=" "$env_file" | cut -d= -f2-; }

"$here/compose.sh" up -d --wait postgres
network="purrcare_purrcare"
docker run --rm --network "$network" purrcare-migrator:latest \
  --connection "Host=postgres;Port=5432;Database=catecommerce;Username=catecommerce_migrator;Password=$(get MIGRATOR_DB_PASSWORD)"
"$here/compose.sh" exec -T postgres psql -v ON_ERROR_STOP=1 -U postgres -d catecommerce -q \
  -v app_password="$(get APP_DB_PASSWORD)" -v migrator_password="$(get MIGRATOR_DB_PASSWORD)" -v reporter_password="$(get REPORTER_DB_PASSWORD)" \
  -f /opt/purrcare/roles.sql
echo "migrations applied and roles refreshed"
