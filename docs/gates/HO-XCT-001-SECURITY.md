GATE: SECURITY
HANDOVER: HO-XCT-001
COMMITS: c68d4bbe 61805d27 c9050c84 b5ade66e e8f685e4 d38ba82e 202241b4 (range a8761829..202241b4, branch ho/XCT-001-build)
DATE: 2026-10-12
STATUS: PASS

No BLOCKER or HIGH findings. Two MEDIUM and four LOW findings, listed below; they do not block this gate (owner rule 2026-09-27) and are
written as proposed rows for docs/backlog/XCT.md. The highest existing row there is 14. The numbers below (15-20) are only proposals: the
ARCHITECTURE and CODE_REVIEW gates run at the same time and may propose rows too, so the integrator gives the final numbers when the rows are merged.

FINDINGS
- [MEDIUM] deploy/backup/schedule.cron:6, deploy/backup/scripts/export-erasure-journal.sh, docs/runbooks/backup-and-restore.md:56,66 -- erased customers can come back after a point-in-time restore.
  Rule: no erased customer may reappear after a restore (ADR-314, D-11). The journal is written once a day at 01:30. Section 4 of the runbook
  restores with `--delta`, which overwrites the current data directory, and nothing tells the operator to export the journal first.
  Scenario: logical corruption is found at 15:00, and the operator restores to a target from two days earlier. Any customer who asked to be
  deleted between 01:30 and the restore is in no journal. That customer's account comes back Active, and no ledger points to it. The
  self-heal does not catch it either, because it only looks for Anonymised accounts. The personal data then stays, permanently and silently.
  A second problem: section 4 step 4 says to reconcile "before the application is started". But section 5 step 3 reconciles through the API,
  so the app (with its notification and retention workers) has to be running while the restored, not-yet-erased customers are live.
  Remediation:
  (a) Add a step 0 to section 4: if the current database is still readable, run export-erasure-journal.sh, or the same SELECT with
      `requested_on > <target>`, against it BEFORE restoring.
  (b) Export the journal more often, ideally on the same interval as the archive check (10 min), so the gap matches the RPO.
  (c) Say that the app is started with background workers disabled (Operations:RetentionWorker:Enabled=false, notification worker off)
      until the reapply has run. Or give a CLI/one-off reapply path that needs no HTTP.
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Operations/EraseCustomerOnRequestCommandHandler.cs:90-91 -- free text about the erased person goes into the immutable audit log.
  `Reason` and `IdentityCheck` are free text (up to 500 characters each). They are written into the audit row's reason and afterValue, and
  audit.audit_entry is protected against UPDATE and DELETE. Staff will naturally write how they checked identity, for example
  "NID 1990xxxxxxx, DOB 12/03/85, called back 017xxxxxxxx". That stores the erased person's identifiers in a table that can never be erased
  or redacted, which defeats the erasure the row records.
  Remediation: make IdentityCheck an enumeration, for example RegisteredMobileCallback, RegisteredEmailReply, InPersonPhotoId or
  WrittenRequestSigned, and store only the code. Either drop the free-text Reason from the audit row, or keep it on the ledger as a field
  that can be redacted. At the least, warn in the UI and the validator that no identifiers may be typed.
- [LOW] deploy/backup/scripts/export-erasure-journal.sh:49 -- the S3 secret key is passed on the command line.
  `curl --user KEY:SECRET` puts REPO2_S3_KEY_SECRET in argv. Anyone who can list processes can read it: inside the container
  (/proc/<pid>/cmdline) and on the host (`ps`, `docker top`).
  Remediation: give curl the credentials through a config on stdin instead: `curl --config - <<EOF ... user = "$REPO2_S3_KEY:$REPO2_S3_KEY_SECRET" ... EOF`.
  Also, the production render of pgbackrest.conf, which holds repo2-s3-key-secret and repo2-cipher-pass, is not defined anywhere. The drill
  renders it with plain envsubst under the default umask (0644). Document or script a `umask 077` / `chmod 600` render for production.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Operations/ReapplyErasuresAfterRestoreCommandHandler.cs:65 -- the bulk reapply audit row does not say which accounts were anonymised.
  POST /api/admin/retention/erasures/reapply irreversibly anonymises up to 500 accounts per call. It takes only a free-text restoreReference:
  no identity check, and no check that a restore happened or that the ids come from a journal. Its one audit row holds counts only. A rogue
  or compromised administrator account can mass-erase active customers this way. Per-account DataChange rows from change capture still exist,
  but the business event does not name the accounts.
  Remediation: put the processed id list (ids are not personal data) in the audit row's afterValue. Consider refusing ids whose account has
  logged in or ordered after the restoreReference date, or require that a restore test or backup record is referenced.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Operations/DatabaseRoleGuard.cs:33 -- the role guard misses some roles that can reach the table owner.
  `pg_has_role(current_user, tableowner, 'USAGE')` only catches inherited membership. A login role granted membership in
  catecommerce_migrator with NOINHERIT (or INHERIT FALSE on PG16+) passes the guard. That role can still `SET ROLE catecommerce_migrator` and
  `ALTER TABLE ... DISABLE TRIGGER trg_refuse_hard_delete`. Exploiting this needs SQL execution as the app role, so it is defence in depth.
  Remediation: use 'MEMBER' instead of 'USAGE' (and on PG16+ also check the SET option).
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Validators/WebVitalCommandValidators.cs:15 -- anonymous callers can send any route value to the telemetry beacon.
  The anonymous endpoint accepts any lowercase path that matches the regex. It is rate limited (30/min per IP), capped at 4 KB and stores no
  identity, but there is no list of known route templates. Distributed callers can skew the p75 Pass/Fail verdicts, add unbounded distinct
  routes to the 93-day summary scan, and store concrete paths that carry ids or phone-like digit strings if a client ever sends a raw path
  instead of a template.
  Remediation: check RouteTemplate against a server-side list of the SPA's route templates (or normalise digit and GUID segments to `[id]`).

Checked and clean:
- Authorization on every new endpoint (RequireAction, action-to-role table in ActionRoles.Operations.cs):
  - erasure and reapply, backup status, restore tests, and the manual retention run: Administrator only.
  - retention reads: Administrator and Accountant. These reads return ids, statuses and codes, and no personal data.
  - web-vitals summary: Administrator and ShopManager.
  - offline snapshot: front-desk staff roles. The branch must be in the caller's assignment (HttpContextActiveBranchAccessor refuses a
    branch outside it). The appointments section needs Appointments.View. The response is no-store/private, and each issue is audited.
  - ping and telemetry are the only public endpoints. Both are rate limited, and the policies are registered.
  - Telemetry ignores the caller's identity, so nothing personal is stored, and every validation failure returns the same generic error,
    so the endpoint cannot be used to probe which field was wrong.
- SQL injection: the trigger migrations interpolate only compile-time schema and table constants. The web-vitals summary SqlQueryRaw is
  fully parameterised. DatabaseRoleGuard is parameterised. In report-run.sh, psql `:'var'` quoting is used for every value. The journal SELECT
  is static. In restore-drill.sh, the only interpolated value is a date the script itself generates.
- Retention purge: RetentionPurger deletes only from five tables, each of which must be classified Purgeable (checked at run time and by a
  test). No table name comes from outside. Forbidden tables (clinical, financial, ledger, audit) carry BEFORE DELETE and BEFORE TRUNCATE
  triggers, and the EF interceptor refuses them before any SQL is sent. Purge sources without a rule (stored-file clinical purposes) are not
  scheduled. Every retention setting has a minimum of at least 1 month or 7 days, so no period can be set to zero.
- Erasure coverage: there is an eraser or a documented exemption for every context holding a customer id (U-B6). Accounts with no ledger
  are self-healed. Eraser exceptions are logged with ids and the exception type only.
- Secrets: nothing is committed (drill passwords are generated per run). Secrets are injected through environment variables or psql
  variables. Backup errors are scrubbed before they are shown to administrators. repo2 is encrypted on the client side, and the off-site
  copy of the journal is encrypted too.
- The test-only controller is in the test assembly only, so it is never reachable in the shipped host.

SCOPE CHECK
- files in allowed scope: 190/190 (src/**, tests/**, deploy/**, docs/runbooks, docs/backlog/XCT.md, docs/test-cases/TC-HO-XCT-001.md, .ai/decisions/adr.md per handover)
- files outside allowed scope: none found by this gate (the ARCHITECTURE gate owns the detailed scope check)
- this gate wrote only docs/gates/HO-XCT-001-SECURITY.md; no tests were run (static review; memory constraint), catecommerce_dev untouched
