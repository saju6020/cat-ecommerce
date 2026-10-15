#!/usr/bin/env bash
# Drill-only entrypoint: renders the drill pgBackRest configuration from the environment, then hands over to the official PostgreSQL entrypoint.
set -euo pipefail

umask 077
/opt/purrcare/scripts/render-config.sh /drill/pgbackrest.drill.conf.template /etc/pgbackrest/pgbackrest.conf
exec docker-entrypoint.sh "$@"
