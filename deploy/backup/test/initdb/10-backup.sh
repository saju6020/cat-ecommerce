#!/usr/bin/env bash
# Runs once, while the official entrypoint initialises the data directory: switch WAL archiving on (the production settings, from the image).
set -euo pipefail

echo "include = '/etc/postgresql/postgresql.backup.conf'" >> "$PGDATA/postgresql.conf"
