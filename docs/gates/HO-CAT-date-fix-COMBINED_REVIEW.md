GATE: COMBINED (ARCHITECTURE + SECURITY + CODE_REVIEW; gate scope rule, small follow-up)
HANDOVER: HO-CAT-date-fix
COMMITS: 5c0898e1, dd77c067 (range 587ea317..dd77c067)
DATE: 2026-10-10
STATUS: PASS

FINDINGS
- [MEDIUM] src/Read/CatEcommerce.Read/QueryHandlers/Visit/ClinicalHistoryQueryHandlers.cs:117 — the clinical-history vaccination status still computes "today" as `DateOnly.FromDateTime(now.UtcDateTime)`, the same UTC defect on a read path that duplicates the Cat status query (which this branch fixed). Between 00:00 and 06:00 Dhaka it shows a vaccination due today as "due tomorrow" / overdue boundaries one day off. Fix: inject `ICatRecordClock` and use it. Backlog CAT D2.
- [LOW] src/Application/.../Validators/CatRecordDateBound.cs:9-10 — XML doc still says dto.Today comes from `IInventoryClock.LocalTodayAsync(MainBranchId)`; it is now `ICatRecordClock`. Code has no Inventory/Shop dependency (grep: no IInventoryClock/ShopBusinessDate in any Cat file); only the comment is stale. Backlog CAT D3.
- [LOW] tests/CatEcommerce.Application.Tests/TestSupport/FakeInventoryPorts.cs:142 — `FakeCatRecordClock` is a Cat fake placed in the Inventory ports file; move to its own `FakeCatPorts.cs`. Backlog CAT D3.
- [LOW] Validator/aggregate message drift in the lenient window — `RecordCatVaccinationCommandValidator` says "A vaccination cannot be recorded with a future date."; the aggregate says "The vaccination date cannot be in the future." A date that is future locally but <= UTC+14 date now reaches the aggregate and gets the aggregate wording/violation shape instead of the validator one. Other six messages are identical. Edge case only (date >= tomorrow local, entered by a client that bypasses the web date cap); no code change in codes. Backlog CAT D3.
- [LOW] Test coverage — endpoint test covers only weight; CatRecordClock itself (non-default zone, fallback when branch row missing) has no direct test; handler tests inject a fake so the zone read is exercised only via the one endpoint test. Backlog CAT D3.

CHECKS PERFORMED (no finding)
- Module boundary: ICatRecordClock is Cat-owned in Domain/DomainServices; CatRecordClock in Infrastructure/Persistence reads Branches via the shared DbContext (precedent: CareReminderSweep, BranchClosure handlers). No IInventoryClock / ShopBusinessDate in any Cat file (remaining hits are Inventory's own).
- Read -> Domain port: GetCatVaccinationStatusQueryHandler already imported Domain.DomainServices and uses IBusinessSettings; consistent with precedent. Read still AsNoTracking, no writes.
- DI: CatRecordClock AddScoped (depends on scoped DbContext + singleton TimeProvider) - correct; injected only into scoped handlers/query handler. Validators now take TimeProvider (singleton), registered as IValidator<T> in CatProfilesModule; no manual `new XValidator()` call sites left in src.
- Strictness: every future-date path has an aggregate check against dto.Today: Register/Update DOB (CatAggregate ValidateDateOfBirth), neuter (:120), vaccination (:272), parasite (:320), deceased (:352), weight (CatWeightRecordAggregate:31). The lenient UTC+14 bound cannot let a future date reach storage; aggregate rejects it, and the handler maps DomainRuleViolationException to a 400 violation.
- Error codes/messages unchanged (see LOW drift above for one wording).
- Allergy RecordedOn (UpdateCatMedicalProfile) has no future-date check on old or new code; not a regression, not a "today" use. Out of scope.
- Security: no new input surface, no authz change, no secrets; CatRecordClock query is parameterised EF.
- Tests would fail on old code: CatDateBoundTests (constructor signature and UTC date at 19:00Z) and CatLocalDateEndpointTests (old code returned 400 for 2026-10-10 at 19:00Z) both fail pre-fix.
- Merge: `git merge-tree --write-tree master HEAD` -> clean (exit 0, tree 01ca83c5), no conflicts with master e6eeb192. No migrations touched.

SCOPE CHECK
- files in allowed scope: 29/29 (Cat handlers/validators/port/clock, one Read query handler, Cat module, tests, CAT backlog)
- files outside allowed scope: none
