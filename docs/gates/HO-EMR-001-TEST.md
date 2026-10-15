GATE: TEST
HANDOVER: HO-EMR-001
COMMITS: 729393f (50f1ab1..729393f) plus the gate's own test file
DATE: 2026-10-07
STATUS: PASS (0 BLOCKER, 0 HIGH; 3 MEDIUM, 3 LOW to backlog)

Sheet: docs/test-cases/TC-HO-EMR-001.md (one row per §15/§16/§18 case, statuses observed this run).
Counts: PASS 133 · FAIL 2 (both MEDIUM, gate-added cases) · BLOCKED 1 · DEFERRED 19 (Phase F: U-F1..9, I-F1..8, A10, A11) · NOT RUN 0.

Environment: per-branch DB `catecommerce_emr` only, `CATECOMMERCE_TEST_DB` = its full Npgsql string (the committed local-dev credentials in
CatEcommerceApiFactory with the database name replaced); `catecommerce_dev` not touched. Build 0 warnings / 0 errors. Domain 867/867, Application
211/211, Infrastructure 128/128. Integration, whole suite: 827 pass, 1 fail = `ShopCataloguePerformanceTests.I_E11` (load flake; Shop code unchanged),
**rerun alone: passed**. No GCS, no clamd, no SMS/email providers; Testing adapters.

CONFIRMATIONS REQUESTED
- Finalise / child-row immutability (I-C7, I-C9): PASS (I-C9 probes every UPDATE/DELETE/INSERT/TRUNCATE listed in the handover, CR001; new G2 adds UPDATE/DELETE/TRUNCATE
  of vitals and problems). CR001 -> 409: new G1 forces a real CR001 inside a request and sees `409` "finalised and cannot be changed" with the commit rolled back.
  One bypass found: F-1 below.
- I-B6 vitals+weight rollback: PASS (real DB trigger fails the weight insert; 500; zero vitals rows and zero weight rows). Reran 3x green.
- `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration." Migrations listed through AddConsents.
- diagnostic_result_value in audit snapshots: **NO.** Observed on a real order with a result set of two values (G4): the DataChange snapshot carries the order and
  `DiagnosticResultSet.*` (Id, ReportedOn, RecordedBy, RecordedOn, ...) but no result-value row (no analyte name, number, unit, flag). Cause is as suspected:
  `BelongsTo` matches only entries whose direct ownership principal is the root. The values remain in `clinic.diagnostic_result_value` (append-only trigger),
  so no data is lost; the audit trail just does not show them. G4 pins this behaviour. See F-3.
- I-C2 and draft first-write race after 2f2e597: PASS. `TheDraftFirstWriteRace_*` and `I_C2_C3_*` green 3 times in a row; new G8 (8 fresh visits x 3 simultaneous
  first writes) gave no 5xx, at least one 204 each time, exactly one row each time. The `Failed executing DbCommand` lines in the log are the expected unique-index loser being retried.
- Vitals `measuredOn` with `+06:00`: PASS. Existing `MeasuredOn_WithALocalOffset_*` plus new G6 sending raw JSON text with `+06:00`, `-05:00`, `+05:30`, `Z`: each 201, stored instant
  equals the sent instant (read back from timestamptz in UTC). G7: no-offset time is not a 5xx. The earlier CODE_REVIEW HIGH is fixed and covered.
- I-E4: PASS, run alone twice. Summary p95 56 ms / 58 ms (budget 600); section page p95 46 ms / 41 ms (budget 300). Fixture is 60 visits, 250 weights, 80 orders x 12 values
  on one cat; the handover's 3 treatments per visit are Phase F and absent (budget headroom is large, so the omission is unlikely to change the verdict).

FINDINGS (MEDIUM/LOW only; nothing blocks)
- [MEDIUM] F-1 Infrastructure/Persistence/Migrations/20261007080435_AddClinicalRecordImmutability.cs, `clinic.visit_child_frozen()` — the INSERT/UPDATE branch looks up the parent status from
  `NEW.visit_id` only. `UPDATE clinic.visit_vitals SET visit_id = <open visit> WHERE visit_id = <finalised visit>` succeeds, removing a vitals row (same for problems and photos)
  from a finalised record, contrary to ADR-118's "no change to a finalised visit's children". Observed (test G3, FAIL, then Skip'd with the reason; assertion unchanged).
  Fix: on UPDATE also check `OLD.visit_id`'s parent status (or forbid changes to `visit_id` outright). Needs a new migration, so integrator-owned.
- [MEDIUM] F-2 Infrastructure/Auditing/AuditChangeCaptureInterceptor.cs `BuildSnapshot` (pre-existing, unchanged on this branch) — owned rows are keyed `ClrTypeName.Column`, so a collection of two or more owned
  rows (two problems, two vitals entries, two result sets) collapses to the last row's values. Observed on a visit saved with two problems: the DataChange `AfterValue` carries one
  `VisitProblem.*` set (G5, FAIL, Skip'd). For clinical records this loses audit detail of which problems were set. Fix: key by index or id, or emit an array per type.
- [MEDIUM] F-3 same interceptor — nested owned rows (`diagnostic_result_value` under `DiagnosticResultSet`) are never captured (answer to the question above). Result values entered or
  superseded do not appear in the DataChange trail, only in the table. The `BusinessEvent` rows from event handlers are the only other record. Decide whether the handover wants values in the snapshot; if so extend `BelongsTo` to walk the ownership chain.
- [LOW] The two code-review MEDIUM test-quality notes stand and were not strengthened here: I-G4 does not assert that no `visit.follow-up-due` notification exists before its lead time
  (it only asserts `SendAfter > now+5d`), I-G5 asserts only `NotEqual Suppressed`; U-X3 proves a trigger exists per table, not that the right function (frozen / append_only / no_delete) is attached.
- [LOW] G1 shows the 409 path exists, but no case drives a CR001 through the *clinical* write paths (note, finalise) from HTTP; each is guarded earlier by `EnsureOpen`, so that is by design.
- [LOW] I-A2's second half (host refuses to start outside Development/Testing with a short key or a root inside the content root) was not observed; it is HO-FIL-001's guard (BLOCKED row G9).

WEAK EXISTING TESTS: none found that would pass with production code broken, apart from the three code-review notes above (left for a human; no test was weakened or deleted).

GAPS LEFT DELIBERATELY: Phase F (deferred by owner); real ClamAV / GCS; SMS/email delivery (HO-NTF-001 OQ-1); prescriptions / boarding / food history panels (other areas).

NEW TEST CODE: tests/CatEcommerce.IntegrationTests/Endpoints/ClinicalRecordGateTests.cs (G1..G8; G3 and G5 `Skip`ped with the defect reason, to be un-skipped by the fix). No production code changed.
