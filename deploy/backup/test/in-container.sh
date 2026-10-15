#!/usr/bin/env bash
# Drill-only helper, run in a ONE-OFF container that mounts the drill volumes (the main container is stopped or killed): renders the drill pgBackRest
# configuration, then runs the given command line. usage: in-container.sh <command> [args...]
set -euo pipefail

umask 077
/opt/purrcare/scripts/render-config.sh /drill/pgbackrest.drill.conf.template /etc/pgbackrest/pgbackrest.conf
exec "$@"
