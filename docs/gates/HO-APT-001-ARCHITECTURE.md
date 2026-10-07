GATE: ARCHITECTURE
HANDOVER: HO-APT-001
COMMITS: 8d3c08d c1696d5 acc5d47 (fix range bcc73ed..HEAD; bcc73ed is the second-pass record). Full branch 2bdc066..acc5d47, branch ho/APT-001, 130 files
DATE: 2026-09-28
STATUS: PASS

This is the **third pass** of the ARCHITECTURE gate. The second pass (recorded in bcc73ed) failed on two HIGH findings, A and B. Both came from the first fix round. All five first-pass HIGH findings had already been confirmed resolved and are not re-opened.

What this pass did:
- Reviewed `git diff bcc73ed HEAD` line by line: 14 files.
- Re-scoped the full branch with `git diff 2bdc066 HEAD`.
- Re-read these sources:
  - ADR-068 and ADR-070, including the new amendment;
  - earlier ADR amendment entries in `.ai/decisions/adr.md` (lines 154, 203, 287), to check the convention;
  - HO-APT-001 §1 Amended (8) and (9), §11.4, §12.4, §12.5, §13 and §14;
  - `Program.cs` for filter registration, and `Hosting/SafeExceptionHandlerMiddleware.cs`;
  - `Background/ClinicWorkCycle.cs`.
- Ran `dotnet build CatEcommerce.sln --no-incremental`: it succeeds with 0 warnings and 0 errors.
- Ran no tests. Anything that depends on runtime behaviour is marked for TEST.

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail the gate. MEDIUM and LOW findings go to docs/backlog/APT.md, which this gate does not edit. Backlog rows 3–20 are not raised again.

SECOND-PASS HIGH FINDINGS — RESOLUTION
A. **Stale holds committed in their own save** — RESOLVED, as the owner decided on 2026-09-28: keep the separate save as a recorded ADR-070 deviation.
   - **The ADR amendment follows the convention.** It is appended under ADR-070 as "**Amended 2026-09-28 (owner decision, …)**" with a **Decision:** line (adr.md:2772-2782).
     - The original Decision text is left untouched, as §13's "append only" requires, and Status stays Active.
     - The amendment states the reason (EF does not order `Modified` rows, and exclusion constraints are checked per statement), why the change is safe (expiring a lapsed hold is idempotent), and the rule it trades for: no pending tracked changes before the helper runs.
   - **§1 Amended (8) records it** and points to the ADR amendment.
   - **The rule is enforced in code.** `SlotClaim.cs:164-171` throws `InvalidOperationException` on entry when `IStaleHoldReleaser.HasPendingChanges()` returns true. The implementation, `StaleHoldReleaser.cs:18`, uses `ChangeTracker.HasChanges()`, which runs `DetectChanges`. So it also catches an outbox row that was enqueued but not yet saved.
   - **All five callers comply.** I re-verified C1 (AppointmentBookingCommandHandlers.cs:58), C4 (AppointmentChangeCommandHandlers.cs:54), C6 and C7 (AppointmentStaffCommandHandlers.cs:51 and :123) and C15 (WaitlistCommandHandlers.cs:148). Before the helper runs, each one only reads, loads aggregates as tracked-but-unmodified, validates and builds a DTO. None of them mutates, adds or enqueues anything.
   - **C15 runs in a fresh DI scope per dispatch** (`ClinicWorkCycle.cs:43`). So no leftover tracked state from an earlier worker command can trip the guard.
   - **The two stale "one save" comments are corrected:** AppointmentBookingCommandHandlers.cs:54-57 and StaleHoldReleaser.cs:9-14. `IStaleHoldReleaser`'s own summary is updated too.
   - **Layering of `HasPendingChanges()` on the Domain port is acceptable, so there is no finding.**
     - The Domain interface exposes only a `bool`. No EF Core type crosses into Domain, and the `ChangeTracker` call stays in Infrastructure.
     - Domain ports carrying unit-of-work semantics is established, gated precedent in this codebase: `INotificationEnlister` (it enlists roots into the caller's save), `INotificationOutbox` and `IStockLedger` (they enlist without saving).
     - The only consumer is the Application helper that owns the rule, which is the right place for the check to run.
     - A generic unit-of-work probe would sit more cohesively on its own port. That is a style preference, not a documented convention, so it is not raised.
B. **Global `40P01` → generic `409`** — RESOLVED, as the owner decided on 2026-09-28: scope the mapping to slot-claim endpoints and map it to `409 SlotNoLongerAvailable`.
   - **The mechanism is sound.**
     - `SlotClaimEndpointAttribute` is a behaviour-less marker, `AttributeUsage(Method)`.
     - MVC copies action attributes into the endpoint's metadata.
     - `UniqueConstraintExceptionFilter` is an MVC `IExceptionFilter` registered globally (Program.cs:63). It runs after routing, so `HttpContext.GetEndpoint()` is the selected action's endpoint. It reads the marker there (UniqueConstraintExceptionFilter.cs:108).
     - The mapping reuses the same `Conflict(SlotNoLongerAvailable, ClinicMessages.SlotNoLongerAvailable, "StartsOn", "AppointmentAggregate")` shape that the customer-route `23P01` branch produces (compare :155-158). This restores ADR-068 and the §12.4 C1/C4 contract, and HO-APT-002:217's branch on the code.
   - **All HTTP slot-claim paths are marked.**
     - The four dispatch sites of `PlaceSlotHoldCommand`, `RescheduleAppointmentCommand`, `BookAppointmentByStaffCommand` and `MoveAppointmentCommand` are the four marked actions:
       - `CustomerAppointmentsController.PlaceHold` (:25);
       - `CustomerAppointmentsController.Reschedule` (:88);
       - `StaffAppointmentsController.Book` (:91);
       - `StaffAppointmentsController.Move` (:110).
     - **C15 confirmed:** `OfferWaitlistSlotCommand` is dispatched only from `Background/ClinicWorkCycle.cs:38`. No controller references it, so there is no HTTP endpoint to mark.
     - The other callers of `SlotClaim` are exactly C1, C4, C6, C7 and C15.
     - Walk-ins (C8) and C11 Start do not go through `SlotClaim` and stay unmarked, as the owner decision says ("slot-claim endpoints only").
   - **A deadlock on an unmarked endpoint is still logged.**
     - The filter leaves `ExceptionHandled` false and returns (:114), so MVC rethrows.
     - `SafeExceptionHandlerMiddleware` (Program.cs:186) then logs it at Error level at SafeExceptionHandlerMiddleware.cs:65 (`"Unhandled exception on [{Method}] {Path}"`) and answers `500`.
   - **Tests.**
     - I-C2 (AppointmentBookingFlowTests.cs:114-122) now asserts `SlotNoLongerAvailable` in every loser's body, not only the status code.
     - `UniqueConstraintExceptionFilterTests` covers three cases: a marked endpoint → handled; an unmarked endpoint → not handled; and the wrapped `InvalidOperationException` shape.
   - **Scope.** This narrows what the §13-listed filter does rather than widening it. The new marker file is recorded in §1 Amended (9).

Also observed:
- Backlog row 15, the doubled `---` at the end of `adr.md`, is fixed in 8d3c08d. The removed separator was this branch's own; the file at 2bdc066 ended with a single `---`.
- No residue from the superseded architecture was introduced: no broker, `IDispatcher`, Mongo, `202`, rejection event or projection. No `*.csproj`, package or migration change is in bcc73ed..HEAD.

FINDINGS
- [LOW] src/Api/CatEcommerce.Api/Filters/SlotClaimEndpointAttribute.cs:13 and src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs:101 — the comments say "exactly the **five** slot-taking/slot-claiming actions", then list and mark four (C1, C4, C6, C7). C15 is correctly excluded as worker-only. Handover §1 Amended (9) correctly says four.
- [LOW] src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs:102-104 and HO-APT-001 §1 Amended (9) — these say a `40P01` on a marked action maps to "the exact shape a `23P01` on the appointment exclusion constraints produces". That holds only on customer routes. On the staff Book and Move routes, a `23P01` maps to the specific `PerformerBusy`/`RoomBusy`/`CatAlreadyBooked` (:154-156), while a `40P01` maps to `SlotNoLongerAvailable`.
  - This is the owner's decision and fits §12.4's "409 stale version or race" for move.
  - But the staff calendar in HO-APT-002 must treat `SlotNoLongerAvailable` as a possible staff `409` too.
- [LOW] tests/CatEcommerce.IntegrationTests/Unit/UniqueConstraintExceptionFilterTests.cs:73-82 — the unit test builds endpoint metadata by hand. So nothing pins that MVC actually surfaces the attribute on the four real actions, or that a future slot-claiming action carries it. A missing marker would silently regress a deadlock loser to `500`.
  - I-C2 exercises PlaceHold end to end, but only when Postgres happens to deadlock.
  - A convention test enumerating the `SlotClaim`-calling actions would close this. Marked for TEST.

SCOPE CHECK
- files in allowed scope: 125/130. Each counted file is one of the following:
  - inside §13;
  - an ADR-341 per-area translation or a CONTRACTS C-ALL file, as accepted in the first pass;
  - recorded in §1 Amended (1)–(9). This pass adds `src/Api/CatEcommerce.Api/Filters/SlotClaimEndpointAttribute.cs`, which is outside the literal §13 list, recorded in (9), and the owner-directed implementation of decision B;
  - a test under `tests/**` (§13). This pass adds `SlotClaimTests.cs`, `FakeStaleHoldReleaser.cs` and `UniqueConstraintExceptionFilterTests.cs`;
  - `.ai/decisions/adr.md`: §13 allows it, and the ADR-070 amendment is appended within this handover's ADR-067…076 range;
  - a process artifact: `docs/backlog/APT.md`, this gate file, and the `docs/handover/HO-APT-001.md` §1 Amended row (accepted practice, HO-NTF-001-ARCHITECTURE pass 2).
- files outside allowed scope: 5. All five are unchanged since the first pass and are carried as backlog row 12 (MEDIUM, owner-routed), so they are not raised again:
  - src/Api/CatEcommerce.Api/Background/CareReminderSweep.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Notifications/EfNotificationOutbox.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Configuration/ClinicOptions.cs
  - src/Api/CatEcommerce.Api/RateLimiting/AppointmentRateLimitPolicies.cs
- The fix commits touch no §14 path: no `web/**`, no `.claude/**`, no `.ai/**` other than `decisions/adr.md`, no `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`, and no other area's aggregate.

NOTES FOR LATER GATES
- **TEST.** Run the suite against a worktree-local database, never `catecommerce_dev` (backlog row 2). Check four things:
  - I-C2 with its new body assertion: every loser must carry `SlotNoLongerAvailable`;
  - `SlotClaimTests` and `UniqueConstraintExceptionFilterTests`;
  - that `AppointmentStaleHoldReleaseTests` (C4, C6, C7 and C15 over an expired hold) still pass with the pending-changes guard in place;
  - the second pass's outstanding item: the detail `statusHistory[]` projection against Postgres.
- **TEST.** Consider a convention test that the `SlotClaim`-calling actions carry `[SlotClaimEndpoint]` (LOW above).
