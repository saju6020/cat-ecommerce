GATE: TEST
HANDOVER: HO-ADM-001
COMMITS: source/test-bearing head at task start `649e8a0` (branch `ho/ADM-001`); this gate's own new
tests are uncommitted on top of it (listed below — the integrator commits). Branch head advanced to
`fdfdece` (CODE_REVIEW's own sixth-pass PASS verdict, doc-only) while this gate was in progress;
nothing in it affects this gate's scope or findings.
DATE: 2026-09-26
STATUS: PASS

Test-case sheet: `docs/test-cases/TC-HO-ADM-001.md`. Written first (before any test was run), from
HO-ADM-001 §15/§16 in full, every §18 acceptance criterion, every Amended (1)-(37) row, and the items
the ARCHITECTURE/SECURITY/CODE_REVIEW fifth-pass gates flagged by name for TEST to confirm. Executed
after.

## Counts

| | Count |
|---|---|
| Total (§15/§16/new/regression rows) | 100 |
| PASS | 83 |
| FAIL | 0 |
| BLOCKED | 17 |
| NOT RUN | 0 |

Full suite, both full-solution runs after this gate's own additions were fixed and stable:
`CatEcommerce.Domain.Tests` 190/190, `CatEcommerce.Application.Tests` 68/68,
`CatEcommerce.Infrastructure.Tests` 28/28, `CatEcommerce.IntegrationTests` 275/275 — **561/561, twice,
identical.** No flakiness observed. `dotnet build`: 0 errors, 0 warnings.

## What this gate did

1. Read `HO-ADM-001.md` in full (§1 Amended history through §20), and all three fifth-pass gate
   verdicts (`docs/gates/HO-ADM-001-{ARCHITECTURE,SECURITY,CODE_REVIEW}.md`), specifically for the
   items each named as owed to TEST:
   - the endpoint-access convention test (U-A1) — found, green, and its own two fixture tests prove
     the scanner itself catches an inherited-unmarked-action shape;
   - the `Policies.Manage` 403 tests — found (`ShopManager_CannotPublishPolicyVersion_...`,
     `ShopManager_CannotCreateOrPublishAnyPolicyDocument_AdministratorCan`) and green, but **no
     existing test had ever searched the audit log for the `AuthorizationDenied` row I-A3 explicitly
     promises** — written this gate (below);
   - the global NUL filter's cross-area effect on Accounts (sign-in 400 not 401, registration NUL
     rejected before CAPTCHA) — ARCHITECTURE pass 5's own MEDIUM 1 named this as unverified by any
     committed test; SECURITY pass 5 ran ad-hoc probes but none became a repeatable test — written
     this gate (below);
   - blank `placement` — already fixed and tested in the sixth fix round (`feb1f32`), before this
     gate started; re-run, green;
   - `pageSize`/`pageNumber` caps — found, green, on both the leave-requests and audit-search
     controllers;
   - clinical-audit redaction unless `Cats.View` — found, green, including the multi-role
     (Vet+Accountant) case and the four `Cat`-labelled `BusinessEvent` rows;
   - maintenance mode — found (`MaintenanceMode_NeverBlocksAdminApiOrStaffSignIn`), green;
   - branch-assignment refusal — found (`ActiveBranchAccessEndpointTests`, 5 cases), green;
   - leave-overlap DB rule (the GiST exclusion constraint race) — found
     (`TwoOverlappingLeaveRequests_SentConcurrently_OnlyOneSucceeds`), green;
   - calendar edges — found (`BranchCalendarTests`, including the two-disjoint/three-overlapping
     partial-closure BLOCKER fix and both London DST edges), green.
2. Wrote `docs/test-cases/TC-HO-ADM-001.md` before running anything, mapping every §15/§16 row to an
   existing test (file + name) or marking it a gap, from reading the test tree, not from running it.
3. Wrote the tests the gates named as missing (tests/** only, no source change):
   - `tests/CatEcommerce.IntegrationTests/Endpoints/AdminAuthorizationDenialAndNulFilterCrossAreaEndpointTests.cs`
     (new file, 8 tests): the `AuthorizationDenied` audit row after a `Cats.MarkDeceased` denial and
     after a `Policies.Manage` denial (I-A3); the NUL filter's effect on customer/staff sign-in (400,
     not the generic `invalid_credentials` 401) and on customer registration (400 before
     `CaptchaRequired`/`CaptchaInvalid`), plus a same-mobile clean-retry proving no account was
     created and no lockout side effect.
   - `AdminAuditEntriesEndpointTests.cs` (+3 tests): the CSV-injection guard actually exercised through
     a real export (U-E1, all four guarded characters `= + - @`), and the `AuditLogExported` row
     actually searched for (I-E2) — the pre-existing `ExportCsv_ReturnsCsvAndAuditsTheExport` asserted
     the CSV response only, despite its own name.
   - `AdminContentPolicyEndpointTests.cs` (+1 test): accepting a policy version that has been
     superseded (U-F5, `PolicyVersionNotCurrent`) — no existing test tried to accept anything but the
     then-current version.
4. Ran everything twice against `catecommerce_adm` (see below for the connection-string correction),
   updating the sheet one row at a time as each result was observed, not from memory afterward.
5. Filled every §15/§16 row honestly, including the ones with no test: **17 BLOCKED rows**, each with
   a reason and (where one exists) what would unblock it. Two of those are worth the acceptance gate's
   attention specifically:
   - **A11 (ADM-05, "changes take effect without a restart") has zero test coverage on either of its
     two named consumers** (`clinical.vaccinationDueSoonDays`, `security.staffIdleTimeoutMinutes`,
     I-D3/I-D4). Both are global, non-branch-overridable settings; the shared `catecommerce_adm`
     database every other integration test also runs against has no test-collection isolation that
     would make a *permanent* mutation of either safe against a concurrently-run, unrelated test class
     that assumes the registry default. Writing the case for real, as specified, risks introducing
     exactly the kind of shared-state flakiness this repo's own §16.1 rules exist to prevent. Not
     written; flagged as a real gap on a named acceptance criterion, not silently dropped.
   - **I-E3 (export over the cap → `413`) cannot be executed as specified.** `AuditExportRules.MaxRows`
     is a hard-coded `const int = 50_000`, not configurable by a test factory — §16's own case
     description ("lower the cap in the factory") assumes a seam that does not exist in the
     implementation. Generating 50,001 real rows for a fast test is impractical. This is a genuine
     implementation/test-design mismatch, not a bug in the 50,000 figure itself.

## FAIL: none

No case, existing or new, failed against the final commit. Two things did fail during execution and
are recorded rather than smoothed over (both explained in full in the sheet's Environment section):
- Setting `CATECOMMERCE_TEST_DB` to the bare database name (as this gate's own instruction phrased
  it) rather than a full Npgsql connection string produced 225/275 integration failures, all a
  malformed-connection-string artifact, zero of them a product defect. `CatEcommerceApiFactory` reads
  the variable as the whole connection string. Corrected and re-run.
- This gate's own new `N1` theory case (the `=` variant) initially failed: the assertion didn't
  account for `AdminAuditEntriesController.Escape` correctly doubling an embedded `"` in the CSV cell
  in addition to prefixing the leading `'` — the product was right, the test's expected string wasn't.
  Fixed by using a quote-free decision note. Rebuilt (0 warnings), reran clean.

## Existing tests judged weak, and what was strengthened

- `ExportCsv_ReturnsCsvAndAuditsTheExport`'s name promises two things ("and audits the export") its
  own body never checks — it asserts the CSV response shape and headers only. Would still pass if
  `RecordAuditLogExportCommand` were never dispatched, or if the CSV-injection guard were deleted
  entirely. Not renamed or rewritten (renaming a pre-existing test outside this gate's asked-for scope
  felt like more surgery than warranted), but its gap is now closed by two new, separately named
  tests (N1/N2) that would fail if either claim in the original name were false.
- No other existing Administration test was found to be a "decoration" in the sense of passing
  regardless of whether the production code were broken — the ones read in full (all fifth/sixth-round
  regression tests, the redaction tests, the authorization tests) each assert a specific status code
  and/or a specific field value that a broken implementation would visibly fail.

## Gaps left deliberately (full list and reasoning in the sheet)

Beyond I-D3/I-D4/I-E3 above: the literal exhaustive I-A2 matrix (18 endpoints × 8 roles × 3 caller
shapes) is covered by a large, well-chosen sample, not the full cross-product §19 names by name;
several `StaffAvailability` domain-service rules (2, 4, 5, 7) and a handful of aggregate-level failure
paths (U-C2, U-C4-C7, U-C9, U-D1, U-D5, U-D7, U-A2, U-A3) have no dedicated test at any level; I-F3
(banner visibility window) and I-X1 (log-capture harness) are absent. None of these was introduced by
the sixth fix round this gate reviewed — they are pre-existing depth gaps in Administration's own unit
suite, named individually in the sheet rather than folded into a single line, so ACCEPTANCE/the owner
can decide which (if any) justify reopening this handover versus tracking as debt for a later change
that touches the same files.

## Files changed by this gate (uncommitted, for the integrator)

- `docs/test-cases/TC-HO-ADM-001.md` (new)
- `tests/CatEcommerce.IntegrationTests/Endpoints/AdminAuthorizationDenialAndNulFilterCrossAreaEndpointTests.cs` (new, 8 tests)
- `tests/CatEcommerce.IntegrationTests/Endpoints/AdminAuditEntriesEndpointTests.cs` (+3 tests)
- `tests/CatEcommerce.IntegrationTests/Endpoints/AdminContentPolicyEndpointTests.cs` (+1 test)
- `docs/gates/HO-ADM-001-TEST.md` (this file)

No source file under `src/**` was changed. No existing test was weakened, deleted, or had its
assertion loosened.

## Verdict

**PASS.** Zero FAIL. Every BLOCKED row has a named reason and, where one exists, what would unblock
it — none is hidden inside a passing count. The items the three earlier gates flagged by name for
TEST are all now backed by a green, repeatable test, including the two (`AuthorizationDenied` audit
row, NUL-filter cross-area effect) that had none before this gate. The acceptance gate's attention is
specifically drawn to A11's two BLOCKED rows (I-D3/I-D4) and to I-E3's test-design mismatch, since
neither is a simple "write the test" gap.
