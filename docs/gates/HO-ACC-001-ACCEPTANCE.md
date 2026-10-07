GATE: ACCEPTANCE
HANDOVER: HO-ACC-001
COMMITS: branch `ho/ACC-001` at `dcc0ee0`. Fix delta reviewed line by line: `5be2458` `ed92088` `913d204` (`git diff 279a825..913d204`), plus TEST's `dcc0ee0` (docs only)
DATE: 2026-09-25
STATUS: PASS

HISTORY
- First pass, `279a825` (reviewed head `e07a2d8`): **FAIL**.
  - FINDING 1 (HIGH, A28): `AuditChangeCaptureInterceptor` recorded wrong before-values for
    inserts, replaced owned value objects and removed owned-collection members.
  - FINDING 2 (MEDIUM, scope): an undeclared `InternalsVisibleTo` in `CatEcommerce.Api.csproj`.
  - LOWs: `AuthController` read `DateTimeOffset.UtcNow`; I-A6 asserted neither IP nor cat; I-A7's
    evidence was indirect; no test observed a non-null audit IP.
  - The other 35 criteria were MET at `e07a2d8`.
- Second pass, this file (reviewed head `dcc0ee0`): **PASS**. Every first-pass finding is resolved
  below. Owner decisions of 2026-09-25 are recorded in HO-ACC-001 §1 Amended (20).

INDEPENDENTLY RE-RUN BY THIS GATE AT `dcc0ee0` (working tree clean before and after)
- `dotnet build CatEcommerce.sln --no-incremental`: 0 warnings, 0 errors.
- `dotnet test CatEcommerce.sln --no-build`: **388/388 passed**. That is Domain 161,
  Infrastructure 28, Application 65 and Integration 134. The 378 from the first pass plus 10
  new `AuditImmutabilityTests` cases give 388, which matches TEST and the developer.
- Read-only SQL against `catecommerce_dev` (`psycopg2` session `readonly=True`). It covered the
  DataChange rows (`category = 1`) that this gate's own run wrote (`CreatedDate` ≥ 22:19:00+06,
  about 460 rows):
  - `CustomerSignInFailedEvent`: 21 rows, 0 with before == after. Sample diff:
    `LoginThrottle.FailedCount` 0 → 1, `LastFailedOn` null → value.
  - `DeliveryAddressRemovedEvent`: 2 rows, 0 with before == after. The removed address
    (`Line1 "222 Second Street"`, its `Id`, etc.) is present in *before* and absent from *after*.
  - `DeliveryAddressAddedEvent`: 14 rows, 0 with before == after. The new address is absent from
    *before* and present in *after*.
  - Inserts: every insert has `BeforeValue` NULL. That is 80/80 `CustomerRegisteredEvent`, 5/5
    `CatRegisteredEvent`, 31/31 `StaffAccountCreatedEvent`, and the session-issue rows.
  - Anonymisation: 2/2 have `BeforeValue` NULL (A33 unchanged).
  - `xmin` appears in 0 rows.
  - Non-null IP: 3 `CustomerRegisteredEvent` rows and 1 `CatRegisteredEvent` row. These are the
    new `WithFixedRemoteIpAddress` tests.
  - Exactly 2 rows in this run have before == after. Both are legitimate:
    - a `CustomerMergedEvent` whose paired BusinessEvent says `IsReapplication=True`. The §8.9
      step 5 re-send is a genuine no-op.
    - one `CustomerSessionAggregateChanged` whose only change is `CsrfTokenHash`. That field is
      redacted to `"[redacted]"` on both sides by design (A34).
- Nothing was written to the database outside the committed test suite.

RESOLUTION OF FIRST-PASS FINDINGS
- FINDING 1 (HIGH, A28): **RESOLVED.** Read in `AuditChangeCaptureInterceptor.cs:51-88,136`.
  - `Deleted` entries are now collected.
  - Root candidates are still restricted to `Added`/`Modified`. That is safe: `grep` finds no
    `.Remove(`, `RemoveRange` or `ExecuteDelete` on any aggregate in `src/Infrastructure` or
    `src/Application`.
  - *before* excludes `Added` owned entries, and is `null` for an `Added` root.
  - *after* excludes `Deleted` owned entries.
  - `CollectProperties` now reads `OriginalValue` whenever `useCurrent` is false.
  - `BelongsTo` still matches a `Deleted` owned entry by its FK. The real rows above prove this
    (the removed address reaches *before*).
  - U-A8 is no longer weak. TEST reverted the interceptor to `e07a2d8` and 6 of the 13 new
    content tests failed, each on its own assertion. With the fix restored, 13/13 pass. The tests
    assert content (`Assert.Contains`/`DoesNotContain` on specific old and new values), not just
    that a row exists.
- FINDING 2 (MEDIUM, scope): **RESOLVED by owner authorisation.** Amended (20)(c) records the
  `InternalsVisibleTo` in `CatEcommerce.Api.csproj` as authorised on 2026-09-25. The edit itself
  is unchanged: one assembly, test-only.
- LOW, `AuthController` clock: **RESOLVED** (`ed92088`).
  - `GET /api/auth/session` now uses `AsOf = timeProvider.GetUtcNow()` from the injected
    `TimeProvider`, the same singleton that `SessionAuthenticationMiddleware` uses.
    `ValidateOnBuild` plus I-A1 prove the new constructor parameter resolves.
  - No `DateTimeOffset.UtcNow` remains in any Accounts source. The remaining `DateTime.UtcNow`
    reads are in the Cat validators and aggregate, which belong to HO-CAT-001. They are not in
    this delta.
- LOW, I-A6 assertions: **RESOLVED.**
  `Registration_ProducesADataChangeAuditRowInTheSameTransactionWithIp` now registers a customer
  and a cat through `WithFixedRemoteIpAddress`. For both, it asserts `Before` is null, `After` is
  populated and `IpAddress` equals the fixed peer.
- LOW, I-A7 evidence: **RESOLVED.** Two direct tests now exist:
  - `AuditIp_WithATrustedProxy_ReflectsTheForwardedAddressNotTheProxys`;
  - `AuditIp_WithoutATrustedProxy_IgnoresTheForwardedHeader`.
- LOW, no non-null IP observed: **RESOLVED.** The tests above observe it, and so do the real
  rows. The `IStartupFilter` in `AccountsApiFactory` is test-only and does not touch production
  configuration.
- LOW, TC-sheet bookkeeping: **RESOLVED.** TEST's second pass records 181 PASS / 0 FAIL /
  5 BLOCKED / 0 NOT RUN. It adds U-A10/U-A11 for `1975810`, I-A9–I-A16 for the new tests, and
  R15/R16.

R15 (BLOCKED: no controlled-clock test for `ed92088`) — RULING: blocks nothing in §18
- The first-pass item was a LOW, not tied to any §18 criterion. No §18 row cites R15.
- The change swaps one clock read for the injected `TimeProvider`. Under `TimeProvider.System`
  it behaves identically, and I-C1/I-C6/I-C8 still pass. Its only effect is agreement under a
  fake clock.
- Reading the one-line diff is sufficient evidence that the source is correct. The missing
  regression test is carried as a recommendation (below), not a condition.
- R16 (PASS with caveat) is the same situation for `fdec8b0`, which the first pass already
  reviewed and ruled correct.

AMENDED (20) — RULING
- (20)(a): **Ratified.** The description matches the code and tests reviewed above, and the
  real-row evidence confirms it.
- (20)(b): **Ratified** as an owner decision. DataChange before- and after-values record personal
  data in full, per FR-ACC-12. The tension with D-11 is acknowledged, and HO-XCT-001's eraser is
  carried as a condition.
- (20)(c): **Ratified** as an owner authorisation. It closes FINDING 2.
- First-pass rulings on (1)–(19) stand unchanged.

FINDINGS (second pass)
- [NOTE, non-blocking, no §18 criterion affected]
  `AuditChangeCaptureInterceptor.cs:118-119` keys owned properties by
  `ClrType.Name + "." + property`.
  - If two members of the same owned collection change in one `SaveChanges`, the second
    overwrites the first in the snapshot.
  - No Accounts operation does this today. The default address is a root scalar
    (`DefaultAddressId`, observed correct), and adds and removes touch one member at a time.
  - This predates the fix and did not regress. Recommend keying collection members by their
    `Id` if a future multi-member edit is added.
- [NOTE] `TC I-A15`: `NoAuditRow_EverMentionsXmin` uses a substring match. A `FullName`
  containing "xmin" in the shared DB could make it fail spuriously. TEST has already recorded this
  as a test-fragility note. It is not a product defect.

CRITERIA (HO-ACC-001 §18)
- A1–A27 and A29–A36: **MET**, with the evidence recorded in the first pass (`279a825`). Every test
  cited there passed again in this gate's 388/388 run.
  - The delta since `e07a2d8` touches only these files: the interceptor, the `AuthController`
    session query clock, test files, and docs.
  - No criterion other than A28 depends on the interceptor's before/after content. A33's
    anonymisation path (`isAnonymisation → before = null`) is preserved, and 2/2 of this run's
    rows show it.
  - A25 stays MET, provisional on SRS Q-16 / OQ-2.
- A28: **MET**. The evidence for each clause:
  - Actor, action, entity, timestamp: source `AuditChangeCaptureInterceptor.cs:95-107`, observed
    in every row.
  - Same transaction: source `SavingChangesAsync`, with `context.Add(auditEntry)` on the same
    context (first-pass ruling on I-A8, unchanged).
  - IP: I-A6 and the two I-A7 tests assert it, and 4 real rows carry it.
  - Before/after content: U-A8 and I-A9–I-A16, shown failing on the pre-fix code and passing now.
    This gate's real-row read confirms it for inserts, replaced value objects, and
    owned-collection adds and removes.

PRIOR GATES (§19, from `docs/gates/` and `docs/test-cases/`)
- ARCHITECTURE: PASS (fifth pass, `2ff0a6d`).
- SECURITY: PASS (third pass, `2ff0a6d`).
- CODE_REVIEW: PASS (it also reviewed `64883ec..df6739f`).
- TEST: PASS, second pass, `dcc0ee0`: 181 PASS / 0 FAIL / 5 BLOCKED / 0 NOT RUN. The 5 BLOCKED
  rows are I-A8, I-F6, I-J5, I-X2 and R15. None is counted as a pass, and none blocks a §18
  criterion (first-pass table, plus the R15 ruling above).
- The fix delta `279a825..913d204` was reviewed line by line by this gate.
  - It adds no I/O, sink, endpoint, header handling or dependency.
  - Its one security-relevant consequence is full personal data in audit values, which is an
    owner decision (20)(b).
  - A further SECURITY or CODE_REVIEW pass is not required for this delta.

SCOPE
- §4 out-of-scope work found: none. The delta adds no feature, package or endpoint.
- §20 conditions: none worked around.
- **§20.4 statement, as required:** reset delivery does not exist outside Development. A20 (a
  new staff member's first password) and A25 (walk-in completion) work **only in Development**
  until HO-NTF-001 lands.

UNDECLARED DEVIATIONS
- None. Both first-pass undeclared items are now declared in Amended (20)(a) and (20)(c).

CONDITIONS CARRIED FORWARD
- **HO-XCT-001's `IPersonalDataEraser` must account for audit rows** (Amended (20)(b)). DataChange
  rows now hold prior emails, mobiles and addresses in full, in a table the database forbids
  editing. Anonymisation does not reach earlier rows. This is the D-11 tension the owner
  accepted.
- A25 stays provisional until the owner answers SRS Q-16 / OQ-2.
- §20.4: A20 and A25 work only in Development until HO-NTF-001.
- Test backlog (recipes are in the TC sheet):
  - I-X1/I-X2 suite-wide sweep;
  - I-J5 as a committed test;
  - I-A8;
  - I-A4 TRUNCATE on a scratch DB;
  - I-F6 on an isolated DB;
  - R15, a controlled-clock `AuthController` test (recommended, not required).
- SECURITY's carried items:
  - the application role must not own `audit.*`;
  - ADR-028 and ADR-030 accept residual enumeration and lockout DoS;
  - never set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.
- (18)(e): the dev `Cors:AllowedOrigins` entry exists only in this worktree's git-ignored file.
- Carried from ARCHITECTURE:
  - the `.ai/` guides still prescribe `UseHttpPipeline()`;
  - the unused `Platform.Infrastructure.Host.WebApi` reference;
  - the stale comment at `ConcurrencyExceptionFilter.cs:12-13`.

OWNER DECISIONS BEFORE MERGE
- None new. SRS Q-16 (A25) remains open, but it does not block merge; A25 is provisional.

INTEGRATOR NOTES (unchanged from the first pass)
- Known conflict: `docs/handover/HO-CAT-002.md` §1. Add this branch's text as a new
  `| Amended |` row.
- This branch carries HO-XCT-001 contract paths from `ho/xct-contracts` (`3b5d812`). Merge order
  with HO-XCT-001 is the integrator's call.

SCOPE CHECK
- files in allowed scope: 7/7 paths in `279a825..dcc0ee0`. These are:
  - the interceptor (`Auditing/**`);
  - `AuthController.cs` (§13);
  - two test files (`tests/**`);
  - the handover's Amended row;
  - the TEST gate and TC sheet.
- Whole branch: 412/412 paths, with `CatEcommerce.Api.csproj` now authorised by Amended (20)(c).
- files outside allowed scope: none.
