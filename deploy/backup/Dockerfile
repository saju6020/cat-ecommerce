# HO-XCT-001 section 12.8 -- the backup image: PostgreSQL 17 with pgBackRest from the PGDG repository (already configured in the official image).
# NFR-CMP-3: open source only, no proprietary cloud backup. No secret is baked in; every secret arrives as an environment variable at run time.
FROM postgres:17-bookworm

# pgbackrest comes from apt.postgresql.org (PGDG). The major version is pinned by the base image tag; the package version is whatever PGDG
# currently publishes for bookworm -- the drill test (run-drill-test.sh) is what proves a new version still restores.
RUN apt-get update \
 && apt-get install -y --no-install-recommends pgbackrest jq gettext-base ca-certificates curl openssl \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /etc/pgbackrest /var/lib/pgbackrest /var/spool/pgbackrest /var/log/pgbackrest /opt/purrcare/scripts \
 && chown -R postgres:postgres /etc/pgbackrest /var/lib/pgbackrest /var/spool/pgbackrest /var/log/pgbackrest /opt/purrcare

COPY --chown=postgres:postgres pgbackrest.conf.template /etc/pgbackrest/pgbackrest.conf.template
COPY --chown=postgres:postgres postgresql.backup.conf /etc/postgresql/postgresql.backup.conf
COPY --chown=postgres:postgres schedule.cron /opt/purrcare/schedule.cron
COPY --chown=postgres:postgres scripts/ /opt/purrcare/scripts/
RUN chmod 0755 /opt/purrcare/scripts/*.sh

# Never run as root: every script runs as the postgres user. The schedule (schedule.cron) is installed in the postgres user crontab of the host or the orchestrator scheduler, which runs the scripts with: docker exec -u postgres <container> /opt/purrcare/scripts/<script>.
USER postgres
WORKDIR /opt/purrcare
