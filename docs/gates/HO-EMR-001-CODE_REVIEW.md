GATE: CODE_REVIEW
HANDOVER: HO-EMR-001
COMMITS: b9c80c5 47612a9 baab800 9d9e2d4 d786ba4 9696dbf 5e4cf63 0805bf1 650556a e1c5e10 3b39c31 75d8c46 (50f1ab1..75d8c46)
DATE: 2026-10-07
STATUS: FAIL

Phase F (treatments) is deferred by owner decision and was not reviewed as missing. Build: 0 warnings / 0 errors.
Unit projects run: Domain 867 pass, Application 210 pass, Infrastructure 128 pass. Integration tests and databases not touched.

FINDINGS
- [HIGH] src/Application/CatEcommerce.Application/DataMappers/VisitMapper.cs:15 (flows to Domain/Entities/VitalsEntry.cs:37, config visit_vitals.measured_on timestamptz) — `RecordVitalsCommand.MeasuredOn` is a client-supplied `DateTimeOffset?` and is stored unnormalised. System.Text.Json keeps the offset of "2026-10-07T10:00:00+06:00"; Npgsql rejects any non-zero offset for timestamptz ("only offset 0 (UTC) is supported"), so a valid request passes every aggregate check and then fails at SaveChanges as a 500, after the in-memory visit was mutated. Same defect class the handover just fixed for FollowUpSchedule and that HO-APT-001 §Amended (5) records. No test sends `measuredOn` with a non-UTC offset (only Application test uses TimeSpan.Zero; no integration test sends the field). Fix: `(c.MeasuredOn ?? now).ToUniversalTime()` in the mapper (or in VitalsEntry ctor), plus a handler test and an endpoint test posting "+06:00".
- [HIGH] src/Api/CatEcommerce.Api/Controllers/StaffVisitsController.cs:152 — a controller resolves `CatEcommerceDbContext` (service locator) to `ChangeTracker.Clear()` and catches `DbUpdateException`/`PostgresException` for the draft first-write race. Violates the project rule that controllers only dispatch and return the result and never hold a DbContext, repository or aggregate (architecture/CQRS layering). Fix: move the retry out of the controller, e.g. into the (already async) `UniqueConstraintExceptionFilter` the same way the `ix_visit_appointment` race is answered, or make the handler tolerate the race; if the owner wants this exact shape it needs a recorded ADR exception.

BACKLOG (MEDIUM/LOW, non-blocking)
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/DischargeSummaryAndFollowUpEndpointTests.cs I-G4 — claims "held until its lead time" but only asserts `SendAfter > now+5d`; it never asserts that no `visit.follow-up-due` notification exists before the clock is advanced (a delivery pass before SetUtcNow would prove the hold). I-G5 asserts the second request is merely `NotEqual Suppressed`; assert it is Expanded/Sent.
- [MEDIUM] tests/.../ConsultationConventionTests.cs U_X3 — the per-table regex proves a trigger exists on each table, not that the right function (frozen vs append_only vs no_delete) is attached; U-X5 relies on a hand-kept NonClinicalGets list (it is self-checked, acceptable).
- [LOW] src/Application/.../CommandHandlers/Visit/ConsultationNoteHandlers.cs SaveConsultationNoteCommandHandler — the ExpectedVersion check runs before the aggregate's EnsureOpen, so a stale tab on an already-finalised visit gets StaleVisitVersion (409) instead of VisitFinalised; FinaliseVisitCommandHandler special-cases this, SaveNote does not. Inconsistent.
- [LOW] src/Application/.../CommandHandlers/Visit/FollowUpHandlers.cs SetFollowUpCommandHandler — if the cat cannot be loaded the follow-up is persisted and 204 returned but no reminder is queued, silently. Return CatNotFound before mutating.
- [LOW] src/Read/.../QueryHandlers/Visit/DiagnosticQueryHandlers.cs:42 and Read/ClinicalHistory/EmrSectionProviders.cs:146 — result sets ordered by RecordedOn only; two sets recorded in the same instant tie. Add Id or a stable tiebreak. Value order (Position) is correctly applied on read (DiagnosticQueryHandlers.cs:51, DischargeSummaryQueryHandlers.cs:203); the aggregate's own `Values` is unordered after a load, so no domain code may depend on its order.
- [LOW] tests I-G4/I-G5 `OlderThanEveryOtherTestsRequests` — a workaround for a shared-database ordering effect (oldest-first notification pass over everything). It works but couples these tests to suite pollution; consider a per-test recipient filter in the delivery-pass helper instead.
- [LOW] UniqueConstraintExceptionFilter now implements both IExceptionFilter and IAsyncExceptionFilter; MVC runs only the async member. Correct (the async delegates to OnException), but worth a one-line test that the sync-only mappings still apply through the pipeline.

CHECKED, NO FINDING
- FollowUpSchedule: UTC normalisation correct (Unspecified local time resolved in the branch zone; invalid/ambiguous local times do not throw); U-G1 and handler tests assert the instant.
- Immutability triggers: finalise transition (OLD open, NEW finalised) allowed; children are not modified in the finalise commit; enumeration value interpolated, not literal; stored_file_referenced replaced by AddConsents and restored in Down; consent guard compares jsonb (valid operator).
- EF mapping: owned collections use explicit ValueGeneratedNever Guid keys, indexes named, no customer id on clinical tables; nested result values carry `position`; snapshot diff is view-entity reordering only (all three view entities still present).
- Migrations: only CreateTable/InsertData/Sql for new objects; no alteration of other areas' tables.
- Guard lines in Appointment cancel/complete handlers are minimal and use ExistAsync.
- Open race: unique index + filter answers the loser with the winner's visit.

SCOPE CHECK
- files in allowed scope: all reviewed non-test source files fall under §13 lists (spot-checked against §13; not exhaustively diffed)
- files outside allowed scope: none identified (Files/FilePurposeAccess tests, AppointmentsApiFactory and Clinic handler/test edits are the ADR-133 guard lines and the Lab-result file purpose change recorded in the TC sheet)
