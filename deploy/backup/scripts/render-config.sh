#!/usr/bin/env bash
# HO-XCT-001 gate fix 2026-10-12 (SECURITY low 3, ARCHITECTURE 48) -- renders /etc/pgbackrest/pgbackrest.conf from the template and the environment (the secret
# store injects REPO2_S3_KEY_SECRET, REPO2_CIPHER_PASS and the rest). The rendered file HOLDS those secrets, so it is NEVER written into the target in place:
# it is written to a private temporary file (mode 0600, in the same directory) and then moved over the target, so there is no moment at which the secrets sit
# in a file readable by anyone else, even when the target already exists with a wider mode from an earlier render. usage: render-config.sh [template] [target]
set -euo pipefail
umask 077

template="${1:-/etc/pgbackrest/pgbackrest.conf.template}"
target="${2:-/etc/pgbackrest/pgbackrest.conf}"

temporary="$(mktemp "$target.XXXXXX")"          # created 0600 by mktemp
trap 'rm -f "$temporary"' EXIT
chmod 600 "$temporary"
envsubst < "$template" > "$temporary"
mv -f "$temporary" "$target"
trap - EXIT
