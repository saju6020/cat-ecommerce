# Backup and restore runbook

HO-XCT-001 section 12.8 (US-XCT-05, NFR-AVL-3/4/5, ADR-311..314). Audience: whoever runs the PurrCare production database. Plain steps, no assumed knowledge of pgBackRest.

## 1. What exists

```
                      +------------------------- production host -------------------------+
   app (catecommerce_app) -->  PostgreSQL 17  --archive_command (every WAL segment, at most 60 s old)-->  pgBackRest
                                    |                                                         |
                          nightly full/diff (01:00)                          repo1 (local disk)      repo2 (off-site S3, encrypted)
                                    |                                                         |
                         ops.backup_run (log, INSERT-only role)  <--- report-run.sh ------------+
                      erasure journal (ids + dates, every 10 min)  ---> next to BOTH repositories
```

| Piece | Where |
|---|---|
| Image (PostgreSQL 17 + pgBackRest from PGDG, no root) | `deploy/backup/Dockerfile` |
| WAL archiving settings (`archive_timeout = 60`) | `deploy/backup/postgresql.backup.conf` |
| Repositories, retention (5 full backups each), compression, client-side encryption of repo2 | `deploy/backup/pgbackrest.conf.template` |
| Schedule (server clock **Asia/Dhaka**) | `deploy/backup/schedule.cron` |
| Scripts | `deploy/backup/scripts/` |
| Database roles | `deploy/database/roles.sql` |

Secrets are never in these files. The rendered `/etc/pgbackrest/pgbackrest.conf` DOES hold them (`repo2-s3-key-secret`, `repo2-cipher-pass`), so it is produced by `scripts/render-config.sh`, which writes it with permissions `0600` (owner only); never render it with a plain `envsubst >` under the default umask. S3 credentials are handed to `curl` on stdin, never on its command line.
Secrets are injected from the secret store as environment variables: `REPO2_S3_KEY`, `REPO2_S3_KEY_SECRET`, `REPO2_CIPHER_PASS`, `REPORTER_DB_PASSWORD`, `JOURNAL_DB_PASSWORD`.
**Keep `REPO2_CIPHER_PASS` somewhere that is not the production host and not the repository it protects: without it repo2 cannot be read, by anyone, ever.**

## 2. The recovery argument (why the numbers hold)

* **RPO 15 minutes (NFR-AVL-3).** Every WAL segment is shipped to both repositories. `archive_timeout = 60` forces a segment switch at least every 60 seconds even on a quiet database, so the data that is not yet in a repository is at most about one minute of writes plus the time to ship it. The drill measures this (section 7); it is not assumed.
* **RTO 4 hours (NFR-AVL-4).** A restore is `pgbackrest restore` of the latest full + differential and WAL replay. The drill times the restore from the off-site repository.
* **Backups exist (NFR-AVL-5).** Daily, with a full every Sunday; repo1 and repo2 each keep the five most recent full backups, which with the daily differentials is a window of about 35 days. This is the stated "35-day backup window" residual: a deleted customer's data can live in an older backup for up to that long, which is why the erasure journal exists (section 5).

## 3. Daily checks (two minutes, an administrator)

Open **Operations -> Backup status** (`GET /api/admin/operations/backup-status`).

* `isBackupStale: true` means no successful full or differential backup finished in the last 26 hours. Look at `lastFull` / `lastDiff` and `errorSummary`, then run `backup.sh diff` by hand.
* `isArchiveStale: true` means WAL has not been confirmed archived in the last 15 minutes. **This is the urgent one**: it is the RPO. Run `archive-check.sh` and read the error.
* `isRestoreTestOverdue: true` means the quarterly restore test (section 6) is due.

## 4. Restoring (disaster)

Do not hurry steps 0 to 2.

0. **Export the erasure journal from the CURRENT database first, if it can still be read.** (Gate fix 2026-10-12.) The scheduled journal is written every 10 minutes, but anyone who asked to be deleted since the last export is in no journal, and a restore with `--delta` overwrites the data directory. Run, on the still-readable cluster, `export-erasure-journal.sh` (or the same `SELECT customer_account_id, requested_on FROM ops.erasure_request` with `requested_on > <restore target>`) and keep the file somewhere that is **not** the data directory. If the database cannot be read at all, the newest scheduled journal (at most about 10 minutes old, in either repository) is what you have: say so in the incident record.
1. **Stop the application** and keep it stopped. Decide the target: the moment just before the disaster (`--target "2026-10-12 11:52:00+06"`), or `latest`.
2. **Restore** on the replacement host (the data directory is replaced; there is no undo):
   ```
   restore-pitr.sh --target "2026-10-12 11:52:00+06" --confirm            # from repo1
   restore-pitr.sh --target latest --repo 2 --confirm                       # off-site only, if the host and repo1 are gone
   ```
   The script refuses without `--confirm`, and refuses a data directory whose `postgresql.auto.conf` names the production cluster unless `--i-understand-this-replaces-production` is added.
   **Before the restore:** confirm PostgreSQL is stopped and no postgres process is left: `pgrep -a postgres` must print nothing (inside the container: `ps -ef | grep postgres`). The script itself refuses (exit 4) when `postmaster.pid` names a live process.
   After an unclean stop remove the stale `postmaster.pid` first, **but only after that check shows no postgres process** (the process is dead; pgBackRest refuses to restore over a "running" cluster). A restore that "did nothing" because the file was still there looks exactly like a restored cluster in step 3, so do not skip the checks below.
   **After the restore, before step 3, check all of:** (a) the command exited **0** (`echo $?`) AND printed `Restore to '...' finished` (it prints nothing like that on failure; on failure the pgBackRest error is the last line); (b) the data directory was really rewritten: `grep recovery_target_time "$PGDATA/postgresql.auto.conf"` shows your target for a time restore, and `$PGDATA/recovery.signal` exists.
3. **Start PostgreSQL** and wait for recovery to finish and promote. Check `SELECT pg_is_in_recovery()` is `false`. That alone is NOT proof of a restore (an un-restored cluster also answers `false`), so add a positive proof: (a) the timeline moved on: `SELECT timeline_id FROM pg_control_checkpoint();` is higher than before the incident (a point-in-time restore starts a new timeline), and (b) the newest data is at or before the target: for example `SELECT max("OccurredOn") FROM audit.audit_entry;` is **not after** the target time you gave. If either fails, stop and treat the restore as not done.
4. **Start the application with its background workers OFF** (`deploy/backup/restore-mode.env`: the notification, retention, clinic, orders, inventory, pharmacy and shop workers). It runs as `catecommerce_app` (it refuses to start as an owner or superuser). With the workers off nothing is sent to, and no job acts on, a customer who was erased after the restore point and has not yet been re-erased. Keep the public site in maintenance mode (Administration -> System status) until step 6 if you do not want customers to sign in to restored, un-erased accounts meanwhile.
5. **Reconcile erasures (mandatory, ADR-314)** through the API, as an administrator, following section 5. The API is the only path: this is why step 4 starts the application, with the workers off, and why "reconcile before the application is started" is not possible.
6. **Restart the application WITHOUT `restore-mode.env`** so the workers run again (the erasure worker then clears the rest of each re-erased customer's data), and take a new full backup immediately (`backup.sh full`): the restore started a new timeline.

## 5. Erasure reconciliation after a restore (mandatory)

An erased customer must not reappear because a backup is older than the erasure.

1. A job writes the **erasure journal every 10 minutes**: `customer_account_id,requested_on` for the last 40 days, nothing else. repo1: `/var/lib/pgbackrest/erasure-journal/<date>.csv` (plain). repo2: `erasure-journal/<date>.csv.enc` next to the repository, encrypted with the repository passphrase:
   `openssl enc -d -aes-256-cbc -pbkdf2 -pass env:REPO2_CIPHER_PASS -in <date>.csv.enc`.
   The file of a day is replaced by each export, so the newest one holds the whole 40-day window.
2. Take, in this order: the file you exported in step 0 of section 4; then the newest journal whose date is **after** the restore point's date; if none exists, the newest one before it (it still lists the 40 days before it). Combine them: the union of the ids.
3. Send the customer ids (at most 500 per call) to `POST /api/admin/retention/erasures/reapply` with `{ "customerAccountIds": [...], "restoreReference": "restore of 2026-10-12" }` as an administrator. Repeat for further batches. The response counts `processed`, `alreadyErased`, `notFound`, `merged`. Re-sending an id is harmless.
4. When every batch has been sent, restart the application without `restore-mode.env` (section 4 step 6). The erasure worker then removes the rest of each customer's personal data on its normal schedule.

*Why a file and not pgBackRest:* pgBackRest 2.59 can read a file from a repository (`repo-get`) but has no command to store one, so the export script copies the file beside repo1 and uploads an encrypted copy beside repo2 itself (`curl --aws-sigv4` for S3, credentials handed to curl on stdin; a directory copy for the drill).

## 6. The quarterly restore test (NFR-AVL-5, every 92 days at most)

1. On an **isolated** host or container (never production), with the backup image and the repository credentials:
   ```
   DRILL_ENVIRONMENT=isolated EXPECTED_MIGRATION=<latest migration id> FORBIDDEN_TABLE_COUNT=<n> restore-drill.sh > drill.json
   ```
   It restores the latest backup from repo2, to "five minutes ago", times the restore, runs `verify-restore.sql` (migrations current, audit and retention triggers present, key tables non-empty, audit trail recent, media store skipped until object storage is backed up) and prints JSON.
2. In the application: **Operations -> Restore tests -> Record** (`POST /api/admin/operations/restore-tests`) with that JSON. The system computes the achieved RPO and RTO and decides `Passed` or `Failed` itself: passed only if every check passed, the RPO is at most 15 minutes and the RTO at most 4 hours. You cannot type the outcome.
3. A wrong record is corrected by recording a new one that names the old one (`supersedesId`); nothing is edited or deleted.
4. A `Failed` test does not reset the clock: the next one is still due 92 days after the last **passed** test.

## 7. The automated drill (before every release that touches backups)

`bash deploy/backup/test/run-drill-test.sh` (or `RUN_BACKUP_DRILL=1 dotnet test --filter Category=BackupDrill`) builds the image, restores a throw-away cluster and checks: stanza and archive (I-G1); point-in-time recovery to a recorded instant (I-G2); data loss after an unclean kill, **measured** (I-G3); the drill JSON accepted by the API as `Passed` (I-G4); restore from repo2 alone (I-G5); an unreadable repo2 without the passphrase (I-G6); no literal secret under `deploy/` (I-G7); the erasure journal in both places (I-G8).
It needs Docker and about 15 minutes.

**Known limit of the drill:** no S3-compatible server image could be pulled where it was built, so repo2 is a *posix* repository with the same encryption settings. The S3 transport (endpoint, bucket, key, TLS) is configured but was **not exercised**. Before go-live run section 6 once against the real bucket.

## 8. Key rotation

* **S3 keys:** create new keys, update the secret store, restart the backup job, check the next `archive-check`, then revoke the old keys.
* **`REPO2_CIPHER_PASS`:** a pgBackRest repository cannot change its passphrase in place. Create a new repository (`repo3` with a new passphrase), run a full backup to it, keep the old repository read-only until the new one holds a full backup and WAL, then retire the old one. Record the change and where the new passphrase is kept.
* **Database role passwords:** `psql -v reporter_password=... -f deploy/database/roles.sql` re-applies them; restart the job.

## 9. Object storage (OQ-9)

Uploaded files (photos, lab results) live in object storage and are **not covered by these backups**. The drill reports the `media-store` check as `skipped`, never as passed. Until a second copy is configured (dual-region bucket, or a scheduled copy to another bucket) a loss of the bucket is a loss of the files.

## 10. The read-only journal role

`export-erasure-journal.sh` reads `ops.erasure_request`. The backup reporter role cannot (it is INSERT-only on `ops.backup_run`, by design). Create a dedicated role and keep its password in the secret store:

```
CREATE ROLE catecommerce_journal_reader LOGIN PASSWORD :'journal_password';
GRANT USAGE ON SCHEMA ops TO catecommerce_journal_reader;
GRANT SELECT ON ops.erasure_request TO catecommerce_journal_reader;
```
