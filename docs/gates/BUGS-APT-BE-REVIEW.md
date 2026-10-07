GATE: ARCHITECTURE+SECURITY+CODE_REVIEW (combined, owner rule 2026-10-07)
HANDOVER: BUGS-APT-BE (HO-APT-001 backlog #4, #5, #6, #8, #9, #20, #24, #25, #26, #27, #28, #29, #31, #33)
COMMITS: 974b485 6af38d4 ea5bf38 cf91583 0762503 1b106c8 84eac24 ec17867 (web, #26) 4c0cc7f (test tolerance, #20)
DATE: 2026-10-07
STATUS: PASS

RUNS
- dotnet build: 0 warnings, 0 errors
- Domain tests 686/686, Application tests 145/145
- Integration tests (Appointment|Waitlist|Booking) 56/56 on catecommerce_dev
- web: tsc clean; vitest tests/components/appointments + booking 11 files, 56/56 (node_modules junction removed afterwards)

FINDINGS
- [MEDIUM] src/Application/.../CommandHandlers/Clinic/ClinicCommandHandlerSupport.cs (IsCurrentOwnerAsync) — a cat that cannot be loaded returns true (customer keeps access). Deliberate and documented, but it is fail-open for an ownership check; with soft-deleted cats GetByIdAsync returns null, so a deleted cat re-opens the pre-#25 behaviour. Acceptable today (nothing hard-deletes); record it so a cat-deletion feature revisits it.
- [MEDIUM] src/Infrastructure/.../Scheduling/StaleHoldReleaser.cs:798 TryCommitExpiryAsync — catches any DbUpdateConcurrencyException from the commit callback, not only one for `stale`. The callback does a full SaveChanges, so a conflict on another pending tracked entity in the same context would be swallowed (and only `stale` detached); it would resurface at the handler's own later save, so it is deferred rather than lost. Narrow it: rethrow unless ex.Entries contains the stale entry.
- [LOW] src/Domain/.../Entities/AppointmentStatusChange.cs:6 and Shared AppointmentRules.MaxStatusHistoryEntries — #5 stops trimming but the doc comment still says "bounded at MaxStatusHistoryEntries" and the constant is now only used by a test. Update the comment / remove the constant.
- [LOW] src/Application/.../AppointmentBookingCommandHandlers.cs (Confirm) and WaitlistCommandHandlers.cs (Offer) — #4 relies on a second no-op UpdateAsync purely to publish events. Sound (tracked entity so no Update(); events dispatch after SaveChanges then ClearEvents, so no double publish), but it is an easy pattern to forget at the next ADR-075 two-root save. Audit other multi-root handlers (Cancel with waitlist, Reschedule) and consider documenting the pattern in .ai/domain-events-and-outbox.md.
- [LOW] src/Api/.../StaffAppointmentsController.cs — behaviour change worth a release note: by-id staff actions now return 403 (branch_assignment_required) for an unassigned admin when more than one branch is active. Consistent with ADR-046 / HttpContextActiveBranchAccessor and with the list, queue and walk-in endpoints of the same controller; no legitimate flow found that breaks, but any web admin screen that deep-links an appointment without X-Branch-Id should be checked once a second branch exists.
- [LOW] src/Api/.../AppointmentsModule.cs — Change (20/min per customer) and Performers (60/min per IP) limits are reasonable and use separate counters; the IP partition relies on RemoteIpAddress being the client behind any proxy (same as the existing Availability/Booking policies; no new issue).

VERIFIED, NO FINDING
- #8: every by-id staff handler (Move, Cancel, CheckIn, Triage, Start, Complete, NoShow) and the detail query compare appointment.BranchId to the controller-minted branch; foreign id is AppointmentNotFound -> 404, null branch fails closed (Cancel handler: BranchId null never equals). The cancel validator's reason rule is independent of existence, so no id-existence leak.
- #26: customer view has no customerId, room, triage, complaint, walk-in, channel, version, allowedTransitions or history ChangedBy; BuildCustomerAsync filters CustomerId; web typed MyAppointmentDetail, tsc and tests clean, staff types untouched. PerformerId/PerformerName remain (existing customer-facing feature).
- #4: UpdateAsync only calls Update() when detached; SaveChanges then events then ClearEvents per root, so each root's events publish once.
- #9/#20: soft-deleted rows are still counted as occupancy on purpose: the gist exclusion constraints (ex_appointment_performer/room/cat) have no is_marked_to_delete predicate, so hiding them from availability would offer slots the constraint then rejects. Reasoning is correct. Display reads and finders now filter them and left-join cat/customer/branch.
- #29: assignment lookup matches the scheduling-inputs and roster convention (performer must have a StaffBranchAssignment containing the appointment's branch); room branch check added.
- #33: IsStaff is set from ResolvedCaller.Kind in the staff controller only; the customer controller leaves it false and a client cannot set it.
- #31: ordered by id; swallowed exception is limited to DbUpdateConcurrencyException; ExpireHold and the exclusion constraint still decide the final outcome (SlotNoLongerAvailable).
- #24, #25, #27, #28, #5, #6: as described; no superseded-architecture constructs, IInMemoryDispatcher used, registrations updated for the new view-model type.

SCOPE CHECK
- files in allowed scope: 51/51 (appointments backend, web customer appointment types/mocks/tests, tests)
- files outside allowed scope: none
