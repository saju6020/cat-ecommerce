GATE: TEST
HANDOVER: HO-ACC-001
COMMITS: 913d204 (branch head, ho/ACC-001) — second pass
DATE: 2026-09-25
STATUS: PASS

HISTORY

First pass (`df6739f`): PASS, 174 rows (170 PASS, 0 FAIL, 4 BLOCKED). Flagged U-A8 weak: the sole
committed content assertion for the change-capture interceptor's before/after values only proved a
`DataChange` row existed, not that its content was correct.

ACCEPTANCE (`279a825`), first pass: FAILED A28. `AuditChangeCaptureInterceptor` wrote the new value as
the before-value for inserts and replaced owned value objects, and dropped removed owned entries —
exactly the gap U-A8 had already named. Fixed by `5be2458` (interceptor), `1975810`/`fdec8b0`/`ed92088`
(TimeProvider on `MergedOn` and every audit writer). `AuditImmutabilityTests` grew from 3 to 13 cases;
`AccountsApiFactory` gained `WithFixedRemoteIpAddress`; two new I-A7 tests were added; `AuthController`
now uses the injected `TimeProvider`. Developer reports 388/388, twice.

THIS PASS (second, short and targeted)

1. Build and full suite, once: `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors. `dotnet test
   CatEcommerce.sln` — **388/388** (`Domain.Tests` 161, `Infrastructure.Tests` 28, `Application.Tests`
   65, `IntegrationTests` 134), matching the developer's figure exactly.

2. Independently reproved the 6 content tests non-decorative. In this worktree (not a scratch clone —
   working tree was clean before and after, nothing committed): restored
   `AuditChangeCaptureInterceptor.cs` to its pre-fix content at `e07a2d8` (`git checkout e07a2d8 --
   <file>`), rebuilt clean, ran `AuditImmutabilityTests` — **6 of 13 failed**:
   `AddingASecondAddress_TheNewAddressAppearsOnlyInAfter`,
   `RemovingAnAddress_TheRemovedAddressAppearsInBeforeAndNotInAfter`,
   `ChangingEmail_OldToNew_BeforeHasTheOldValueAfterHasTheNew`,
   `FailedSignIn_RecordsFailedCountGoingFromZeroToOne`,
   `SettingEmail_FromNull_TheNewAddressAppearsOnlyInAfter`,
   `Registration_ProducesADataChangeAuditRowInTheSameTransactionWithIp` — each on the exact content
   assertion its name describes. Restored the fix (`git checkout HEAD -- <file>`), confirmed
   `git status --short` clean, rebuilt clean, re-ran — **13/13 passed**. Matches the brief's "6 of 13"
   exactly.

3. Read-only check of real rows (`SET default_transaction_read_only = on`, `catecommerce_dev`,
   psql 17 client):
   - Email change, old → new: latest `CustomerContactDetailsUpdatedEvent` row's `EmailAddress.Value` is
     the first minted address in `BeforeValue` and the second in `AfterValue`, neither appearing in the
     other's column.
   - Failed sign-in, `FailedCount` n → n+1: latest `CustomerSignInFailedEvent` row shows
     `"LoginThrottle.FailedCount":0` in `BeforeValue`, `"LoginThrottle.FailedCount":1` in `AfterValue`.
   - Non-null IP: multiple rows carry a real peer/forwarded address (e.g. `198.51.100.7`,
     `203.0.113.21`, `203.0.113.10`).
   - No row contains the captured `xmin` token: a regex check for `xmin`/`Xmin` as an actual JSON key
     (`"[Xx]min"\s*:`) returns **zero** rows across all 25,816 rows in the shared table. A plain
     substring `ILIKE '%xmin%'` — the same shape as the test's own `Assert.DoesNotContain` — hits
     exactly one row, inspected directly: `FullName:"No Xmin Please"` from an earlier, uncommitted
     probe, not the concurrency token. Recorded as a latent test-fragility note in the TC sheet (I-A15),
     not a production bug — the actual guarantee (no captured `xmin` property) holds.

4. TC sheet updated: `docs/test-cases/TC-HO-ACC-001.md`. U-A8 flipped from "PASS, caveat" (weak) to
   PASS with the decoration-check evidence above. I-A6 and I-A7 updated (I-A7 now cites two direct
   tests instead of an indirect one). A28 updated to reflect the closed gap (I-A8 remains its own,
   separate BLOCKED row — not addressed this pass, out of this round's scope). Added U-A10/U-A11
   (`MergedOn` TimeProvider threading, both individually watched pass), I-A9–I-A16 (the 8 new/
   strengthened `AuditImmutabilityTests` cases), and R15/R16 for the two TimeProvider fixes named in
   the brief (`ed92088`, `fdec8b0`) — both left honest: R15 is **BLOCKED** (no controlled-clock test
   proves `AuthController` now agrees with `SessionAuthenticationMiddleware`'s clock; the fix was read,
   not watched failing then passing) and R16 is **PASS, caveat** (mechanical, full-suite-green, but
   equally no controlled-clock test exists — under the real clock this suite runs against, the two
   `TimeProvider` implementations are indistinguishable from `DateTimeOffset.UtcNow`, so no test here
   could have caught a regression either way).

COUNTS

186 rows: **181 PASS, 0 FAIL, 5 BLOCKED, 0 NOT RUN** (up from 174/170/0/4/0 at the first pass — net +12
rows, +11 PASS, +1 BLOCKED). Every row added this pass is listed above by ID; nothing from the first
pass was removed or weakened, only U-A8/I-A6/I-A7/A28 were strengthened in place.

SCOPE CHECK

Files touched by this pass, all within `tests/`, `docs/test-cases/` and `docs/gates/`:
- Modified: `docs/test-cases/TC-HO-ACC-001.md`, this file.
- Temporarily restored and re-restored (not committed, working tree confirmed clean before and after):
  `src/Infrastructure/CatEcommerce.Infrastructure/Auditing/AuditChangeCaptureInterceptor.cs`.
- No other file in `src/` was read for the purpose of modification; no other worktree was touched;
  nothing pushed.

VERDICT

**PASS.** 0 FAIL. ACCEPTANCE's A28 finding is closed: the interceptor fix is proven non-decorative by
an independent revert-and-watch-fail exercise (not inferred from the developer's own report), and its
two most safety-relevant claims (an email change's old→new pair, a failed sign-in's counter increment)
are additionally confirmed against real rows in Postgres, read-only. The two TimeProvider fixes named
in the brief are recorded honestly rather than rounded up to PASS: one is BLOCKED for want of a
controlled-clock test, the other is PASS with the same gap flagged as a caveat, since neither could
have been disproven by any test this suite currently runs under a real clock. The 5 BLOCKED rows (I-A8,
I-F6, I-J5, I-X2, R15) are genuine scope/seam limits, not hidden failures, and are recorded for
ACCEPTANCE.
