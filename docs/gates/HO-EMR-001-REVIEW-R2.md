GATE: ARCHITECTURE + CODE_REVIEW (combined follow-up, round 2)
HANDOVER: HO-EMR-001
COMMITS: 2f2e597 729393f (range 75d8c46..HEAD, excluding docs/gates)
DATE: 2026-10-07
STATUS: PASS

Round-1 blockers re-checked: ARCHITECTURE [BLOCKER] controller DbContext + double dispatch, ARCHITECTURE [HIGH] read-side
rehydration, CODE_REVIEW [HIGH] vitals MeasuredOn offset, CODE_REVIEW [HIGH] controller DbContext (same lines as the BLOCKER).
All four are closed. `dotnet build`: 0 warnings, 0 errors. Unit tests: Application 211, Domain 867, Infrastructure 128, all
passing. Integration tests and databases were not touched; their new tests are read, not run.

CLOSURE
1. SaveDraft: StaffVisitsController no longer references CatEcommerceDbContext, Npgsql or EF; it dispatches once and returns
   204 or the failure. The retry is in UniqueConstraintExceptionFilter.TryRetryDraftRaceAsync.
   - Acceptable architecturally: the filter already answers ix_visit_appointment by calling the in-memory dispatcher (a query),
     and it is the documented home of constraint-race mapping (ADR-026). It uses IInMemoryDispatcher (ADR-019). A command
     re-dispatch from a filter is a small step beyond that precedent, but the controller rule (dispatch and return) is met.
   - Cannot loop: an exception thrown by the re-dispatch happens inside OnExceptionAsync and is not routed back through the
     filter; it surfaces as an unhandled 500. Nothing else re-enters the method.
   - Tracker: the failed insert is dropped with ChangeTracker.Clear() on the request-scoped context (the same scope that
     failed), then the handler reloads the winner's row via GetByFilterAsync and takes the UpdateAsync path. Correct.
   - Does not swallow others: it matches only SqlState 23505 with ConstraintName ix_consultation_draft_visit_author AND a
     ConsultationDraftAggregate among the exception entries; everything else falls through to OnException unchanged.
   - Branch safety: the branch comes from IActiveBranchAccessor and the handler still checks visit.BranchId == command.BranchId.
   - Covered by TheDraftFirstWriteRace_BothWritesSucceed_AndOneRowRemains (integration; not run here).
2. Read side: Visit, Diagnostic, DischargeSummary and Consent handlers and the EMR section providers use AsNoTracking + Select
   or SelectMany row projections. No aggregate is rehydrated (the old ActiveProblems / Vitals / ResultSets navigation use is
   gone). Branch (BranchId), IsMarkedToDelete, finalised and owner (OwnerId, checked now) filters are preserved. Orderings are
   preserved and made total with Id tiebreaks; result values are ordered by Position then Id; a correction sorts after the set
   it supersedes. Queries per request are a fixed small number (problems, vitals, photos, addenda, names, orders): the batch
   helpers (VisitProblems.ActiveAsync, DiagnosticRows.*, StaffNames.LookupAsync) take id sets, so there is no N+1.
   - Consent status predicate (Status == Recorded && CatId && AppointmentId && Kind, !IsMarkedToDelete, newest SignedOn first)
     is logically identical to ConsentRules.IsValidFor; the original query already filtered !IsMarkedToDelete outside it.
3. Vitals: VitalsEntry ctor and VisitMapper both call ToUniversalTime(); tests for +06:00 added at handler (unit, passing)
   and endpoint level. Only RecordVitalsCommand.MeasuredOn is a client-supplied DateTimeOffset in the Visit commands and
   contracts; no other unnormalised client instant was found in the EMR commands.

FINDINGS (none blocking; for docs/backlog/EMR.md)
- [MEDIUM] ConsentQueryHandlers.cs GetConsentStatusQueryHandler -- the status predicate now duplicates ConsentRules.IsValidFor
  (the domain rule is no longer called by the status endpoint). They match today but can drift, and HO-SRG-001's contract names
  the rule as its authority. Fix: add a unit test that pins the query predicate against IsValidFor over a status/cat/appointment/
  kind matrix, or build the query from a shared Expression exposed by ConsentRules.
- [LOW] UniqueConstraintExceptionFilter.TryRetryDraftRaceAsync -- a failed retry answers a flat 400 with ValidationResult,
  not the status mapping ClinicalResponses.Failure gives (e.g. visit finalised mid-race). Unlikely and cosmetic; reuse the mapper.
- [LOW] A second draft-race failure is an unmapped 500 (intentional per the doc comment). Acceptable.
- [LOW] The filter reaches into CatEcommerceDbContext and the Application command namespace by fully qualified name; add usings.
- [LOW] VitalsEntry.cs line 36 has a trailing whitespace-only line; adr.md fix of a duplicate `---` is harmless.
- [LOW] ConsultationNoteHandlers: "Open status && version mismatch" means a finalised visit never returns StaleVisitVersion,
  returning VisitFinalised instead (intended; covered by a new endpoint test).

SCOPE CHECK
- files in allowed scope: 15/15 (fix commits touch only Api filter and controller, Application handler and mapper, Domain
  VitalsEntry, Read handlers and providers, tests, and a one-line adr.md blank-separator fix)
- files outside allowed scope: none
