#!/usr/bin/env bash
# deploy/vm -- ONE TIME, on an empty database: restores the release dump (catalogue, stock, settings, the kept admin), creates
# the roles, hands every object to catecommerce_migrator, then runs the migrations (a no-op when the dump is current).
# usage: init-db.sh <path-to-catecommerce_release.dump>
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dump="${1:?path to the release dump}"
env_file="${PURRCARE_ENV:-/opt/purrcare/.env}"
get() { grep "^$1=" "$env_file" | cut -d= -f2-; }
psql_su() { "$here/compose.sh" exec -T postgres psql -v ON_ERROR_STOP=1 -U postgres -d catecommerce -q "$@"; }

"$here/compose.sh" up -d --wait postgres
tables="$(psql_su -At -c "SELECT count(*) FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')")"
[ "$tables" = "0" ] || { echo "the database already has $tables tables; init-db.sh only runs once, on an empty database" >&2; exit 1; }

"$here/compose.sh" exec -T postgres pg_restore -U postgres -d catecommerce --no-owner --no-privileges --exit-on-error < "$dump"
psql_su -v app_password="$(get APP_DB_PASSWORD)" -v migrator_password="$(get MIGRATOR_DB_PASSWORD)" \
  -v reporter_password="$(get REPORTER_DB_PASSWORD)" -f /opt/purrcare/roles.sql
# roles.sql covers the solution schemas; EF's own history table lives in public, so the migrator must own it to record migrations.
psql_su -c 'ALTER TABLE public."__EFMigrationsHistory" OWNER TO catecommerce_migrator'
"$here/migrate.sh"
echo "database initialised"
