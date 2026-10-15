# TC-HO-BRD-001 — Boarding backend (TEST gate)

Handover: docs/handover/HO-BRD-001.md
Commit(s) under test: ho/BRD-001 head cede66fe (gate-fix round 5d6f16fc, fc6ef889, 99a982d8 re-check, cede66fe owner decisions)
Written: 2026-10-15  ·  Executed: 2026-10-15

> Statuses record what was **observed**, not inferred. The Evidence column names what was actually
> seen. `BLOCKED` marks a case that could not be executed or whose result was reasoned about rather
> than watched — it is not a failure, and it is not hidden.
>
> This sheet REPLACES the builder's self-report sheet (it listed every row PASS before any gate ran; it is in git history at 7ddf9852 / 5d6f16fc). Handover section 15/16 case ids are kept; where the
> build numbered a case differently the "Test" named in the Case column is the executable that stands for it. Rows without a Test name had no test at the start of the gate; the gate's
> disposition of each is in Evidence. New gate cases are prefixed T-. Case ids for the handover rows follow the handover; ids for T- rows were assigned by the gate.

## Summary

Executed 2026-10-15 against ho/BRD-001 cede66fe plus the gate's own test commits (the Boarding source is unchanged by the gate; every mutation was restored, `git status` clean after each).
Counts are over the Unit, Integration, Regression, Gate (T-) and Mutation tables below. The Acceptance table is a roll-up and is not counted.

| | Count |
|---|---|
| Total | 216 |
| PASS | 193 |
| FAIL | 0 |
| BLOCKED | 23 |
| NOT RUN | 0 |

The two FAIL rows of the first TEST pass (I-H3b, T-R1b; backlog rows 49 and 50) are now PASS: the lock that first fixed them was removed by the ACCEPTANCE ruling (section 4.4) and replaced by a retry in a fresh DI scope (backlog row 65). Four rows were added after the TEST pass: T-R1c, U-X4b, A41 and I-F3b.
Suite totals: Domain 2217, Application 610, Infrastructure 189 all pass; the full integration run on the fresh database was 1926 tests, 1916 passed, 6 skipped, 4 failed (all four pass alone, see below); the Boarding classes on their own (126 incl. the gate's new ones) are 124 passed, 2 skipped (the two FAIL rows above), 0 failed.

## Environment

- Build: `dotnet build CatEcommerce.sln` green, 0 errors (40 pre-existing analyser warnings in other areas). `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."
- Database: FRESH throwaway `catecommerce_brd_test` on local Postgres 5432 (full connection string in `CATECOMMERCE_TEST_DB`), created by `dotnet ef database update --connection` (all migrations incl. `AddBoardingGateFixes`), dropped at the end of the gate. `catecommerce_dev` and `catecommerce_test` were never touched (the Boarding factory refuses both).
- Memory: no testhost running at start; ~14 GB free of 33 GB; one integration run at a time.
- Full integration suite, ONCE, 11 m 42 s: 1926 tests, 1916 passed, 6 skipped, 4 failed (was 49 failed before the worker-off fix): `BillingTestGateDatabaseTests.XI_6` ("Too few tables held a row to try", order-dependent on an empty DB), `WaitlistFlowTests.IG2_OfferedCustomer_Confirms_BooksTheAppointment_AndClaimsTheEntry`, `ReportingTestGateEndpointTests.T_I7_...` and `ReportingCrossCuttingEndpointTests.I_X1_U_X4_...` (both "The condition did not hold after 600 delivery passes"). Re-run each ALONE: all four PASSED. Three are on the known-flake list; I_X1_U_X4 is not (backlog row 58). No Boarding test failed in the full run (all 100 then-existing Boarding tests passed).
- The gate then added tests (BoardingTestGateTests, BoardingRateLimitTests, BoardingLogCaptureTests, BoardingWorkerOffInOtherHostsTests, Application BoardingConventionTests) and re-ran the Boarding classes: 124 passed, 2 skipped. The gate's new tests were run on top of the same database after the full run, not inside it.
- Hosts started: in-process TestServer hosts only (BoardingApiFactory, BoardingRateLimitApiFactory, a log-capturing derived host, the Accounts host). No Kestrel host and no real worker (workers are driven by hand through `BoardingWorkCycle`).
- A stale-bin artefact to know about: after the mutations the Domain.Tests binary was left mutated until the solution was rebuilt, and showed 1 red Domain test (U_G24) until the clean rebuild; the figures above are from the clean rebuild.

## Unit cases (handover section 15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | `StayTransitionRules` over every pair. Test: U_A1_* (3) | Only appendix arrows; refused names both states | PASS | 83 test(s) passed in Domain.Tests: U_A1 |
| U-A2 | Every transition writes one StatusHistory row; `is_active` per 8.4. Test: U_X3_Occupancy… | Occupancy held Reserved..CheckedOut only | BLOCKED | No test asserts one StatusHistory row per transition. U_X3 (is_active held Reserved..CheckedOut only) PASSED but is half the case. |
| U-A3 | Lapse before DepositDueBy / with PendingApproval / Awaiting. Test: none at start | `ReservationNotLapsable` x2; lapsed | BLOCKED | No unit test. At HTTP I_D2 (lapse after the hold) and I_D4 (PendingApproval suspends the lapse) PASSED; the ReservationNotLapsable code before DepositDueBy is asserted nowhere. |
| U-A4 | Cancel 73 h and 71 h before OccupiedFrom (window 72). Test: none at start | Not forfeited; forfeited | BLOCKED | No unit test for the 73 h / 71 h boundary. I_D6 PASSED for 5 days / 1 day out only. |
| U-A5 | RecordNoShow before and after grace. Test: none at start | `NoShowTooEarly`; recorded | PASS | 1 test(s) passed in IntegrationTests (fresh DB): StaffNeedANoteToCancel_AndANoShowIsRefusedBeforeTheGraceHasPassed; observed at HTTP: NoShowTooEarly before grace, recorded and forfeited after |
| U-A6 | CheckIn the day before arrival / on arrival / day after. Test: none at start (I_E guards the day-before at HTTP) | refused; allowed; allowed | BLOCKED | No unit test. Two of three halves observed at HTTP: before arrival refused (CheckInBeforeTheArrivalDate, T_D2) and on arrival allowed (T_D1, T_D3); the day after arrival is tested nowhere. |
| U-A7 | Confirm with accepted < required / = required. Test: none at start | `DepositNotYetReceived`; confirmed | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M6; observed at HTTP: 899.99 refused DepositNotYetReceived, +0.01 confirmed |
| U-A8 | Eleven templates pass TemplateRules, urgencies. Test: U_A8_* (3) | Pass | PASS | 8 test(s) passed in Domain.Tests: U_A8 |
| U-A9 | checkInWindow 19:00–14:00 refused; registry bounds. Test: U_A9_* (3) | Refused (start < end) | PASS | 3 test(s) passed in Domain.Tests: U_A9 |
| U-B1 | Rate card back-dated / duplicate effective date. Test: RateCards_AreAppendOnly… (HTTP) | `RateCardCannotBeBackdated`; duplicate refused | PASS | 1 test(s) passed in IntegrationTests (fresh DB): RateCards_AreAppendOnly; at HTTP |
| U-B2 | Card at t with two cards: latest EffectiveFrom <= t. Test: RateCards_AreAppendOnly… (HTTP) | Latest wins | PASS | 1 test(s) passed in IntegrationTests (fresh DB): RateCards_AreAppendOnly; at HTTP |
| U-B3 | 5 nights threshold 14. Test: U_B3 | Nightly, no discount | PASS | 8 test(s) passed in Domain.Tests: U_B3 |
| U-B4 | 16 nights weekly 6000 nightly 1000 discount 10 %. Test: U_B4 | 2 weeks + 2 nights, discount per unit | PASS | 9 test(s) passed in Domain.Tests: U_B4 |
| U-B5 | 65 nights. Test: U_B5 | 2 months, 0 weeks, 5 nights in order | PASS | 13 test(s) passed in Domain.Tests: U_B5 |
| U-B6 | 13 vs 14 nights. Test: U_B6 | Nightly vs weekly | PASS | 7 test(s) passed in Domain.Tests: U_B6 |
| U-B7 | 3 cats additional rate 200. Test: U_B7 | AdditionalCat units = 2 x 200 x nights | PASS | 4 test(s) passed in Domain.Tests: U_B7 |
| U-B8 | 60 nights split at 30. Test: U_B8 | Sum of cycles = quote exactly | PASS | 5 test(s) passed in Domain.Tests: U_B8 |
| U-B9 | Deposit fixed 2000 on 1500; percent 30 on 10000; zero. Test: U_B9 | 1500; 3000; NotRequired | PASS | 2 test(s) passed in Domain.Tests: U_B9 |
| U-C1 | Occupancy window defaults on a Dhaka date. Test: U_C1 | Arrival 14:00 local .. 11:00 + 3 h | PASS | 45 test(s) passed in Domain.Tests: U_C1 |
| U-C2 | Back-to-back free; 24 h turnaround not free. Test: U_C2 | Free; not free | PASS | 15 test(s) passed in Domain.Tests: U_C2 |
| U-C3 | Maintenance overlap not free. Test: none at start (I_B2 at HTTP) | Not free | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_B2; at HTTP |
| U-C4 | Two free deluxe units: first by block/order/code. Test: none at start | Deterministic | BLOCKED | No test of the deterministic choice among equal units. |
| U-C5 | 3 cats; capacity 2 and 4 types. Test: none at start (I_C1 at HTTP) | Only the 4-type | BLOCKED | No unit test; I_C1 PASSED (types with capacity >= cats) for 2 cats only, not the 3-cat / capacity 2 and 4 pair. |
| U-C6 | Online request never offered isolation. Test: none at start | Never offered | BLOCKED | No test that an online request is never offered an isolation unit on the availability endpoint (ThePublicUnitTypes... PASSED for the public type list only). |
| U-C7 | Everything full; alternatives. Test: none at start (I_C3 at HTTP) | Alternatives include range | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C3; at HTTP |
| U-C8 | Type without a card omitted. Test: none at start | Omitted | BLOCKED | No test that availability omits a type with no rate card. |
| U-C9 | VaccinationStatusRule reproduces Q4. Test: U_C9 | Identical | PASS | 2 test(s) passed in Domain.Tests: U_C9 |
| U-C10 | Core vaccine due 5 days after arrival, 10-night stay. Test: U_C10 | Blocked DueBeforeDeparture; due after departure passes | PASS | 2 test(s) passed in Domain.Tests: U_C10 |
| U-C10b | Vaccine due ON the last day blocks, day after passes (owner "On or before"). Test: U_C10b | Blocks; passes | PASS | 3 test(s) passed in Domain.Tests: U_C10b |
| U-C11 | Never vaccinated. Test: U_C11 | Blocked NeverAdministered | PASS | 2 test(s) passed in Domain.Tests: U_C11 |
| U-C12 | Override survives re-evaluation. Test: none at start (I_C7 at HTTP) | Overridden | BLOCKED | "Override survives re-evaluation" is tested nowhere; I_C7 PASSED (override and audit only). |
| U-C13 | Reserve with incomplete profile. Test: none at start (I_C8 at HTTP) | CareProfileIncomplete naming cat | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C8; at HTTP |
| U-C14 | Profile with 7 feedings / duplicate time / medication w/o time. Test: none at start | Each refused | BLOCKED | No validator or domain test for 7 feedings, duplicate time, medication without time. |
| U-C15 | 4 cats into capacity 3. Test: none at start (I_C10 at HTTP) | UnitCapacityExceeded | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C10; at HTTP |
| U-C16 | Flagged cat in standard unit. Test: none at start (I_E10 at HTTP) | IsolationUnitRequired | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E10; at HTTP: relocating a flagged stay to a standard unit is refused IsolationUnitRequired |
| U-D1 | Confirm handler with accepted >= required. Test: none at start (no Application-level Boarding tests) | Confirmed, message enqueued | BLOCKED | No Application-level handler test exists (the build shipped none). I_D3 and I_D5 PASSED at HTTP but cannot show "enqueued before the save". |
| U-D2 | Ledger pending approval. Test: none at start (I_D4 at HTTP) | PendingApproval; lapse refused | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D4; at HTTP |
| U-D3 | Lapse handler. Test: none at start (I_D2 at HTTP) | Allocations inactive; message | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D2; at HTTP |
| U-E1 | Task generator, nine tasks. Test: U_E1 | 9 tasks, due instants | PASS | 12 test(s) passed in Domain.Tests: U_E1 |
| U-E2 | Generator twice. Test: U_E2 | Same keys | PASS | 10 test(s) passed in Domain.Tests: U_E2 |
| U-E3 | Medication narrower than stay. Test: U_E3 | Only inside dates | PASS | 6 test(s) passed in Domain.Tests: U_E3 |
| U-E4 | Extended stay. Test: U_E4 | Tasks for added dates | PASS | 6 test(s) passed in Domain.Tests: U_E4 |
| U-E5 | Assigner round robin. Test: U_E5 | Split, none -> unassigned | PASS | 6 test(s) passed in Domain.Tests: U_E5 |
| U-E6 | Complete task twice. Test: none at start (I_E6 at HTTP) | Second no-op | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E6; at HTTP (second tap harmless) |
| U-E7 | Overdue at grace -1 / +1 min. Test: none at start (I_E7 at HTTP) | Not overdue; overdue | BLOCKED | I_E7 PASSED for past due + grace; the minute-before side is not asserted. |
| U-E8 | NotEatingRule. Test: U_E8 | Flagged / not flagged | PASS | 11 test(s) passed in Domain.Tests: U_E8 |
| U-E9 | Check-in checklist missing weight and signature. Test: none at start (I_E1 at HTTP) | CheckInChecklistIncomplete | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E1; at HTTP |
| U-E10 | Belongings, no photos, setting true/false. Test: none at start | Refused / allowed | BLOCKED | No test of the belongings-photos setting true / false. |
| U-E11 | Signature bounds. Test: U_E11 | InvalidSignature each | PASS | 4 test(s) passed in Domain.Tests: U_E11 |
| U-E12 | Flag infectious without target / with target. Test: none at start (I_E9, I_E10 at HTTP) | Refused / relocated | PASS | 2 test(s) passed in IntegrationTests (fresh DB): I_E9, I_E10; at HTTP |
| U-E13 | Isolation entry for a stay not in isolation. Test: none at start (I_E10 second half at HTTP) | NotInIsolation | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E10; at HTTP (an entry needs isolation) |
| U-F1 | Incident severity notifies. Test: U_F1 | Only medium/high | PASS | 28 test(s) passed in Domain.Tests: U_F1 |
| U-F2 | Escalation rule 29/31 min. Test: U_F4, U_F5, U_F6 | Not / escalated; never if acknowledged | PASS | 27 test(s) passed in Domain.Tests: U_F4, U_F5, U_F6 |
| U-F3 | Media RetainUntil; incident-attached not purged. Test: none at start (I_F9 at HTTP) | 12 months; kept | BLOCKED | RetainUntil arithmetic is not asserted. I_F9 PASSED (purge source erases the file, keeps the incident photo). |
| U-F4 | Media type text/html; video 60 MB. Test: U_F9 (caption only) | Refused | BLOCKED | Media type and size are validated by Files, not Boarding; only the caption bound is tested (U_F9 PASSED). |
| U-G1 | Headroom. Test: U_G1, U_G2 | 4000; limit 0 + approvals | PASS | 31 test(s) passed in Domain.Tests: U_G1, U_G2 |
| U-G2 | Commitment twice with one ChargeId. Test: U_G3 | One | PASS | 14 test(s) passed in Domain.Tests: U_G3 |
| U-G3 | Approval answered twice. Test: U_G7 | VetApprovalAlreadyDecided | PASS | 1 test(s) passed in Domain.Tests: U_G7 |
| U-G4 | Extension re-quote crossing the threshold. Test: U_G10, U_G16 | Basis changes, uninvoiced replaced | PASS | 2 test(s) passed in Domain.Tests: U_G10, U_G16 |
| U-G5 | Extension after cycle 1 invoiced. Test: U_G16, U_G17 | Invoiced unchanged | PASS | 2 test(s) passed in Domain.Tests: U_G16, U_G17 |
| U-G6 | Early check-out after 10 of 30 nights. Test: U_G14, U_G18 | Nightly basis, discount gone | PASS | 8 test(s) passed in Domain.Tests: U_G14, U_G18; plus T_M3, T_M3c at HTTP |
| U-G7 | Early check-out 45 of 60, month 1 invoiced. Test: U_G17 | Month 1 untouched | PASS | 1 test(s) passed in Domain.Tests: U_G17; plus T_M3b at HTTP |
| U-G8 | Invoiced exceeds used -> credit. Test: U_G17, U_G22, U_G23 | Credit owed, netted by credit notes | PASS | 6 test(s) passed in Domain.Tests: U_G17, U_G22, U_G23 |
| U-G9 | Relocate without a reason. Test: none at start (I_G6 at HTTP) | RelocationReasonRequired | BLOCKED | The RelocationReasonRequired refusal is not asserted; I_G6 PASSED for a relocation with a reason. |
| U-G22 | (gate) credit netting against credit-note lines. Test: U_G22 | Owed = net - credited | PASS | 1 test(s) passed in Domain.Tests: U_G22 |
| U-G24 | (gate) late check-out charges extra nights, does not extend allocation. Test: U_G24 (name still says "AndExtendsTheAllocation") | Charges; window unchanged | PASS | 1 test(s) passed in Domain.Tests: U_G24; assertions are right; the method NAME still says "AndExtendsTheAllocation" (backlog row 51) |
| U-G25 | (gate) plain-value overloads equal the aggregate's. Test: U_G25, U_G26 | Equal | PASS | 2 test(s) passed in Domain.Tests: U_G25, U_G26 |
| U-G27 | (gate) staff vet-authority change needs a reason, audit event. Test: U_G27 | Reason required; event | PASS | 1 test(s) passed in Domain.Tests: U_G27 |
| U-H1 | Waitlist entry rules. Test: U_H1, U_H2, U_H3 | Refusals | PASS | 65 test(s) passed in Domain.Tests: U_H1, U_H2, U_H3 |
| U-H2 | Daycare 5 h hourly 300 day 200; 3 h. Test: U_H4 | 200 (capped); 900 | PASS | 11 test(s) passed in Domain.Tests: U_H4 |
| U-H3 | Daycare capacity 0. Test: none at start (I_H4 at HTTP) | DaycareNotOffered | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H4; at HTTP |
| U-X1 | No Boarding event carries a string. Test: U_G20 | None | PASS | 1 test(s) passed in Domain.Tests: U_G20 |
| U-X2 | BoardingWorker with a throwing pass is logged, next pass runs. Test: none at start | Logged; continues | BLOCKED | No test of the worker surviving a throwing pass (BoardingWorkCycle is a concrete class; a seam is needed). |
| U-X3 | Migration predicates / is_active per transition. Test: U_X3 | Held Reserved..CheckedOut | PASS | 2 test(s) passed in Domain.Tests: U_X3 |
| U-X4 | No Boarding command handler injects a Billing/Inventory/Clinic/Appointment/Administration repository. Test: none at start | None (ADR-232) | PASS | 1 test(s) passed in Application.Tests: U_X4; new guard BoardingConventionTests (forbidden: Billing, Inventory, Clinic, Administration aggregates; Cat and StoredFile are read by design, see the test comment) |
| U-X5 | No Boarding domain-event handler injects INotificationOutbox. Test: none at start | None (ADR-057) | PASS | 1 test(s) passed in Application.Tests: U_X5; new guard BoardingConventionTests |
| U-Z | Remaining Boarding domain tests not mapped above (U_G11..U_G15, U_G19, U_G21, U_F2/F3/F7/F8, U_H5..U_H9, FilePurposeAccessTests, Infrastructure fakes) | All pass | PASS | Every other Boarding Domain test passed (2217 Domain.Tests, 610 Application.Tests, 189 Infrastructure.Tests, all green on a clean rebuild) |

## Integration cases (handover section 16, real HTTP, fresh DB)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-A1 | Schema, three exclusion constraints. Test: I_A1 | `boarding` schema, `contype='x'` x3 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_A1 |
| I-A2 | Host starts with scope validation. Test: I_A2 | Every registration resolves | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_A2 |
| I-B1 | Admin creates units, rate cards. Test: I_B1 | 201s; nightlyRate shown | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_B1 |
| I-B2 | Availability across maintenance. Test: I_B2 | Excluded for the dates only | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_B2 |
| I-B3 | Maintenance over a reservation. Test: I_B3 | 400 MaintenanceOverlapsStays | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_B3 |
| I-B4 | Nurse POST unit. Test: I_B4 | 403 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_B4 |
| I-C1 | Availability 2 cats 5 nights. Test: I_C1 | Types, prices, totals | PASS | 2 test(s) passed in IntegrationTests (fresh DB): I_C1 |
| I-C2 | Reserved unit disappears; back-to-back; turnaround. Test: I_C2 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C2 |
| I-C3 | All taken -> alternatives/waitlist. Test: I_C3 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C3 |
| I-C4 | 16-night quote weekly + discount lines. Test: I_C4 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C4 |
| I-C5 | Overdue cat blocked, booking link, reserve 400. Test: I_C5 | VaccinationGateBlocked | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C5 |
| I-C6 | After vaccination the same booking proceeds. Test: I_C6 | 200 Reserved | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C6 |
| I-C7 | Only admin overrides, audited. Test: I_C7 | 204 + audit; 403 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C7 |
| I-C8 | Reserve with a profile missing. Test: I_C8 | CareProfileIncomplete names cat | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C8 |
| I-C9 | Three cats one stay. Test: I_C9 | One stay three profiles | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C9 |
| I-C10 | Four cats capacity 3. Test: I_C10 | UnitCapacityExceeded | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C10 |
| I-C11 | Another customer's cat/stay. Test: I_C11 | 404, 404 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C11 |
| I-C12 | 20 parallel reserves, last unit. Test: I_C12 | Exactly one 200, rest 409 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C12 |
| I-C13 | Same cat in two overlapping stays. Test: I_C13 | CatAlreadyBoarding | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_C13 |
| I-C14 | (added) Enquiry holds no unit; booking beyond horizon/past refused. Test: AnEnquiryThatIsNotYetReserved…, OnlineBookingBeyondTheHorizon… | As stated | PASS | 2 test(s) passed in IntegrationTests (fresh DB): AnEnquiryThatIsNotYetReserved, OnlineBookingBeyondTheHorizon |
| I-D1 | Reserve deposit, payBy, message. Test: I_D1 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D1 |
| I-D2 | Hold passes -> lapse. Test: I_D2 | Cancelled/ReservationLapsed | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D2 |
| I-D3 | Deposit in Billing then confirm. Test: I_D3 | Confirmed | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D3 |
| I-D4 | Payment awaiting second approver suspends lapse. Test: I_D4 | Not lapsed; confirmed after | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D4 |
| I-D5 | Deposit recorded in Billing only; worker confirms. Test: I_D5 | Confirmed by worker | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D5 |
| I-D6 | Cancel 5 days out / 1 day out. Test: I_D6, StaffNeedANoteToCancel… | Refund owed / forfeited | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D6 |
| I-D7 | No card/token columns in Boarding tables. Test: I_D7 | None | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_D7 |
| I-E1 | Check-in incomplete then complete. Test: I_E1, CheckInBeforeTheArrivalDate… | 400 list; 200 | PASS | 2 test(s) passed in IntegrationTests (fresh DB): I_E1, CheckInBeforeTheArrivalDate |
| I-E2/E3 | Pass builds task list; twice adds nothing. Test: I_E2_E3 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E2_E3 |
| I-E4 | Extend by 3 nights; tasks for added days. Test: none (U_E4 only) | Tasks added | BLOCKED | No HTTP test that an extension generates tasks for the added days (U_E4 PASSED for the generator; T_M5 for the money). |
| I-E5 | Approve leave; tasks reassigned. Test: none at start | Reassigned | BLOCKED | No test that approving leave reassigns pending tasks. |
| I-E6 | Complete with empty body. Test: I_E6 | 204, caller+time | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E6 |
| I-E7 | Past due+grace overdue. Test: I_E7 | isOverdue | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E7 |
| I-E8 | Two None observations. Test: I_E8 | notEating | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E8 |
| I-E9 | Flag infectious with isolation target. Test: I_E9 | Relocated; entries | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E9 |
| I-E10 | Relocate flagged to standard unit. Test: I_E10 | Refused | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_E10 |
| I-E11 | House-food issue with stay reference. Test: none (HO-IMP-001 side; InMemoryBoardingStayReferenceReaderTests) | BLOCKED expected | BLOCKED | House-food issue is HO-IMP-001 (not merged into this branch); only the in-memory StayReferenceReader fake is tested (InMemoryBoardingStayReferenceReaderTests PASSED). |
| I-F1 | Medium/high incidents notify; low internal. Test: I_F1 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F1 |
| I-F2 | Incident future/blank refused. Test: I_F2 | 400 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F2 |
| I-F3 | Owner acknowledges, own only. Test: I_F3 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F3 |
| I-F4 | Unacknowledged high escalates. Test: I_F4 | escalatedOn, callOwner | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F4 |
| I-F5 | Acknowledged never escalated. Test: I_F5 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F5 |
| I-F6 | Resolve needs resolution. Test: I_F6 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F6 |
| I-F7 | Diary shows photos, hidden disappears. Test: I_F7 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F7 |
| I-F8 | File attaches once. Test: I_F8 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F8 |
| I-F9 | boarding-media purge. Test: I_F9 | Erased, incident photo kept | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F9 |
| I-F10 | Dashboard. Test: I_F10 | In-house, occupancy | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_F10 |
| I-F11 | Dashboard p95 1.5 s with 60 units/50 stays/500 tasks. Test: none | p95 | BLOCKED | No performance test with 60 units / 50 stays / 500 tasks exists; not built by the gate (would need a bulk seeder). |
| I-G1 | Charges pushed at check-in, once. Test: I_G1 | One charge per rate unit | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G1 |
| I-G2 | Early check-out charges nights used. Test: I_G2 | 3000, void rest | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G2 |
| I-G2b | Preview equals result and changes nothing. Test: I_G2b | Equal | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G2b |
| I-G3 | Incomplete check-out. Test: I_G3 | 400 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G3 |
| I-G4 | Extension; stale version 409. Test: I_G4 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G4 |
| I-G5 | Unit booked afterwards -> refused with free units. Test: I_G5 | UnitNotFreeForExtension | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G5 |
| I-G6 | Relocation with a reason. Test: I_G6 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G6 |
| I-G7 | Referral / away / CatNotPresent. Test: I_G7 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G7 |
| I-G8 | Clinic charge headroom, once per charge. Test: I_G8 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G8 |
| I-G9 | Approval above limit. Test: I_G9 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G9 |
| I-G10 | Phone answer needs a note. Test: I_G10 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G10 |
| I-G11 | Surgery on boarded cat. Test: I_G11 | Away, unit held | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G11 |
| I-G12 | Summary owner only. Test: I_G12 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G12 |
| I-G13 | Clear attention needs a note. Test: I_G13 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G13 |
| I-G14 | Long stay invoiced per cycle, final covers the rest. Test: I_G14 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_G14 |
| I-G15 | Customer merge: stays under survivor. Test: none at HTTP (U_H9 for daycare/waitlist) | Survivor | BLOCKED | Customer merge moving STAYS to the survivor has no HTTP test (U_H9 PASSED for waitlist and daycare rows only). |
| I-G16 | Cat deceased with a confirmed stay flags, cancels nothing. Test: ACatDeceasedWithALiveStay… | Flagged | PASS | 1 test(s) passed in IntegrationTests (fresh DB): ACatDeceasedWithALiveStay |
| I-H1 | Waitlist refused while free; accepted when full; once per range. Test: I_H1 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H1 |
| I-H2 | Oldest notified once, no hold. Test: I_H2 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H2 |
| I-H3 | Withdrawn/stale. Test: I_H3 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H3 |
| I-H3b | Daycare capacity 2, three concurrent bookings -> two 201, one 409 | Real parallelism | PASS | Retry in a fresh scope on `ix_daycare_slot` (`ClaimRetry`, up to 4 attempts). 3 of 3 runs PASS (two 201 on different slots, one 409 DaycareFull). |
| I-H4 | Daycare not offered at capacity 0. Test: I_H4 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H4 |
| I-H5 | Daycare slots, cancellation frees. Test: I_H5 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H5 |
| I-H6 | One visit per cat per day, gate applies. Test: I_H6 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H6 |
| I-H7 | Hourly charge capped. Test: I_H7 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H7 |
| I-H8 | No-show timing. Test: I_H8 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H8 |
| I-H9 | Owner scope. Test: I_H9 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H9 |
| I-H10 | Daily update once/day at hour. Test: I_H10 | As stated | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_H10 |
| I-X1 | Log capture across cases: no care notes/descriptions/names. Test: none in Boarding classes | No PII in logs | PASS | 1 test(s) passed in IntegrationTests (fresh DB): I_X1_NoCareNote; new BoardingLogCaptureTests: reserve, check-in, observation, High incident, escalation pass, early check-out through a log-capturing host; no care note, incident text, contact, signer name, customer name or mobile in any line |
| Units-misc | Boarding units: duplicate code, capacity, maintenance overlap, cancelled block, other branch, care routine, public unit types (8 tests in BoardingUnitsEndpointTests without ids) | Pass | PASS | 8 test(s) passed in IntegrationTests (fresh DB): ADuplicateUnitCode, ACapacityAboveWhatTheTypeAllows, TwoOverlappingMaintenanceBlocks, ACancelledMaintenanceBlock, AUnitOfAnotherBranch, TheCareRoutine_IsTheDefault, ThePublicUnitTypes, ARateCardWithMoreThanTwoDecimals |
| Care-misc | `ACaretakerCannotClearTheFlag…`, `AnObservationForACatThatIsNotBoarding…` | Pass | PASS | 2 test(s) passed in IntegrationTests (fresh DB): ACaretakerCannotClearTheFlag, AnObservationForACatThatIsNotBoarding |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCH | BLOCKER .ai/flows edit | Not a test case: `git diff 5a187b6b..HEAD -- .ai/flows` shows only boarding-stay-flow.md added (no edit to another area's flow) | PASS | `git diff 5a187b6b..HEAD --name-status -- .ai` shows only .ai/decisions/adr.md modified; nothing under .ai/flows changed |
| R2 | ARCH | HIGH read-side rehydration | Application/read handlers do not load aggregates: grep `GetByIdAsync` in `src/Read` Boarding handlers; preview uses plain-value `EarlyCheckout.Compute` (U_G25) | PASS | grep of src/Read/.../QueryHandlers/Boarding for GetByIdAsync / IAggregateRootRepository: no match; U_G25 PASSED (aggregate and plain-value overloads agree); preview handler reads columns only |
| R3 | CODE | HIGH worker on in every factory | `Boarding:Worker:Enabled=false` in base factory and appsettings.Testing.json; mutation M8 | PASS | New BoardingWorkerOffInOtherHostsTests PASSED on the Accounts host; with the flag removed from CatEcommerceApiFactory and appsettings.Testing.json it FAILED (mutation M8). No earlier test pinned it. |
| R4 | SEC | M1/M2 deposit earmark scope | I_S1 | PASS | I_S1 and T_M6 PASSED; mutation M1 failed both |
| R5 | SEC | M3 Low incident media in diary / owner ack | I_S3 | PASS | I_S3 PASSED; mutation M6 failed it |
| R6 | SEC | M4 vet-authority reason + audit | I_S2, U_G27 | PASS | I_S2 and U_G27 PASSED; mutation M7 failed U_G27 |
| R7 | SEC | M6 daycare horizon, caps, rate limits | I_S4, I_S12 ; rate limits T-L1..L3 | PASS | I_S4 and I_S12 PASSED; the three rate-limit attributes are now exercised: T_L1..T_L4 PASSED (4th call 429) |
| R8 | CODE | M1 paid gate-blocked stay | I_S5, I_S13 | PASS | I_S5 and I_S13 PASSED |
| R9 | CODE | M2 care task duplicate race | I_S6 | PASS | I_S6 PASSED (two workers generating the same days) |
| R10 | CODE | M3 creditDue netting | U_G22, I_S11 | PASS | U_G22 and I_S11 PASSED; mutation M5 failed U_G22 |
| R11 | CODE | M5 invoiced early check-out preview | I_S8 | PASS | I_S8 PASSED and T_M3b (invoiced early check-out, preview Keep/Void lines) PASSED |
| R12 | CODE | M6 refused extension names free unit | I_S9 | PASS | I_S9 and I_G5 PASSED |
| R13 | CODE | L3 overstay (owner "Charge extra nights, never block") | I_S7, I_S10, U_G24 | PASS | I_S7, I_S10, U_G24, T_M4 PASSED; mutations M3 and M4 failed them |
| R14 | CODE | L4 gate boundary (owner "On or before") | U_C10b | PASS | U_C10b PASSED (dueOffset 0 blocks, +1 passes); mutation M2 failed it |
| R15 | ARCH | L7 refundDue no look-back | I_S5 asserts refundDue list; no test of an old stay (>90 days) | BLOCKED | No behavioural test of an ended stay older than 90 days. GetRefundableAsync was READ and has no date filter; that is reasoning, not observation. |
| R16 | owner | "Show before" preview | I_G2b, I_S8 | PASS | I_G2b, I_S8, T_M3, T_M3b PASSED; mutation M9 failed I_G2b and T_M3 |
| R17 | owner | "Keep everything" erasure | ErasureCoverageConventionTests (XCT) maps Boarding to an eraser; eraser keeps stay (no test of the retention behaviour found) | PASS | New T_E1 PASSED: EraseAsync on a customer with a reserved stay returns Held (more than a year out), a customer with only an enquiry returns Completed, and the stay (status, version) is unchanged |

## Gate cases written by the TEST gate (T-)

Money maths. Fixture for every M row: one Standard unit, rate card nightly 1000.00 / weekly 6000.00 / monthly 24000.00, additional-cat 0 unless stated, long-stay threshold 14 nights (default), earlyCheckoutChargedNights 0, tax 0 %.
Expected figures are written here BEFORE the run.

| ID | Case | Expected (hand-computed) | Status | Evidence |
|---|---|---|---|---|
| T-M1 | 3-night stay A..A+3, 1 cat, discount 0 | Basis Nightly; one Night unit 1-3 = 3000.00; quote total 3000.00; charges Active sum 3000.00 (one charge); deposit required 30 % = 900.00; confirm refused until accepted >= 900.00 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M1 |
| T-M2 | 31-night stay, discount 10 %, 1 cat | Basis Monthly; Month nights 1-30 gross 24000 disc 2400 net 21600; Night 31 gross 1000 disc 100 net 900; quote gross 25000, discount 2500, total 22500.00; deposit 30 % = 6750.00; two charges (21600, 900) | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M2 |
| T-M2b | Same stay, cycle invoice at A+30 10:00 then on-time check-out A+31 10:00, final invoice | Cycle invoice = 21600.00 only (one billed charge); final invoice = 900.00 only; total invoiced 22500.00 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M2; same test: cycle invoice 21600, final invoice 900, sum 22500 |
| T-M3 | 10-night stay (10 000.00), check out after 3 nights (today A+3 10:00) | Early; nights charged 3; basis Nightly; r1 charge (10 000) voided; r2 Night 1-3 = 3000.00 recorded; stay total 3000.00; preview newTotal = 3000.00 and credit 0 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M3 |
| T-M3b | 31-night stay with 10 % discount (22 500), cycle invoice at A+30, then early check-out the same day A+30 10:00 | Early (today < departure A+31); charged 30 nights; basis Monthly; Month 1-30 net 21600 kept (billed); Night-31 charge (900, unbilled) voided; total 21600.00; credit 0 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M3b |
| T-M3c | 31-night discounted stay (22 500), early check-out at A+10 (no invoice) | 10 nights < threshold 14: basis Nightly, discount gone; total 10 000.00 (was 22 500); month charge voided, new Night 1-10 charge 10 000 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M3c |
| T-M4 | 3-night, 2 cats, additional-cat rate 200 (booked 3000 + 600 = 3600), late check-out at A+5 10:00 (2 nights late) | Late; nights charged 5; Night 1-5 = 5000 + AdditionalCat 1 x 200 x 5 = 1000 -> total 6000.00; Active charges sum 6000.00; check-out succeeds (never blocked); allocation range NOT extended (OccupiedTo unchanged); a following booking on the same unit overlapping the overstay is flagged UnitOverstayed | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M4 |
| T-M5 | Extension 5 -> 8 nights (nightly) | Total 8000.00; r1 voided, new Night 1-8 charge 8000 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M5 |
| T-M5b | Extension 5 -> 15 nights crosses threshold | Basis Weekly; Week 1-7 6000 + Week 8-14 6000 + Night 15 1000 = 13 000.00 (was 5 000.00); charges sum 13 000.00 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M5; same test, second extension |
| T-M6 | Deposit: 3-night stay (3000), deposit 30 % | Required 900.00; paid 899.99 -> confirm 400 DepositNotYetReceived; paid +0.01 -> confirm 200; balance due 2100.00 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M6 |
| T-M6b | Deposit earmarked by another customer for 900.00 does not count | Confirm refused; received 0.00 (I_S1 repeated at the boundary of the full amount) | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_M6; same test, first half |
| T-D1 | Dhaka: check-in at 00:10 on arrival date A (UTC A-1 18:10) | 200 CheckedIn (a UTC date would refuse) | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_D1_D2 |
| T-D2 | Dhaka: check-in at 23:50 on A-1 (UTC A-1 17:50) | 400 CheckInNotOnArrivalDate | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_D1_D2 |
| T-D3 | Dhaka: check-in at 23:50 on A | 200 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_D3 |
| T-D4 | Dhaka: 3-night stay, check-out at 23:50 on A+2 (UTC A+2 17:50) | Early; 2 nights charged = 2000.00 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_D4 |
| T-D5 | Dhaka: check-out at 00:10 on A+3 (departure date; UTC A+2 18:10) | NOT early, NOT late; 3 nights = 3000.00 (a UTC date would call it early, 2000) | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_D5 |
| T-D6 | Dhaka: check-out at 23:50 on A+3 | On time 3000.00, not late | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_D6 |
| T-D7 | Dhaka: check-out at 00:10 on A+4 (UTC A+3 18:10) | Late; 4 nights = 4000.00 (a UTC date would charge 3000) | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_D7 |
| T-R1 | Two customers reserve the last unit at once (5 rounds, fresh unit each) | Each round exactly one 200 and one 409 UnitNoLongerAvailable; one active allocation | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_R1; 5 rounds, each exactly [200, 409 UnitNoLongerAvailable] |
| T-R2 | Payment accepted, hold expired; confirm (HTTP) races the worker lapse (8 rounds) | Never Cancelled while a full accepted deposit exists: every round ends Confirmed (or Reserved if confirm lost and worker has not yet confirmed — then a second pass confirms); never Cancelled | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_R2; 8 rounds: confirm POST returned 409 "changed by someone else" in 8 of 8 (the worker confirmed first); final state Confirmed every round; never Cancelled |
| T-R3 | Payment recorded concurrently with worker lapse (8 rounds) | Final status is Confirmed/Reserved, or Cancelled/ReservationLapsed with the payment listed refundable (never lost); allocation inactive iff Cancelled | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_R3; 8 rounds: final state Confirmed every round; the Cancelled+refundable branch was never taken, so that invariant was not exercised |
| T-L1 | Rate limit, reserve: booking limit 3; 3 calls then 4th | 4th 429 for that customer; a second customer not limited | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_L1 |
| T-L2 | Rate limit, waitlist join | 4th 429 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_L2 |
| T-L3 | Rate limit, daycare book | 4th 429 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_L3 |
| T-X4 | Reflection: no Boarding command handler injects a cross-context repository (U-X4) | None | PASS | 1 test(s) passed in Application.Tests: U_X4 |
| T-X5 | Reflection: no Boarding domain-event handler injects INotificationOutbox (U-X5) | None | PASS | 1 test(s) passed in Application.Tests: U_X5 |
| T-W | Worker disabled in another area's host (Accounts host): Boarding:Worker:Enabled false, worker still hosted. Test: BoardingWorkerOffInOtherHostsTests | Disabled | PASS | 1 test(s) passed in IntegrationTests (fresh DB): TheBoardingWorker_IsDisabledByConfiguration |
| T-E1 | Erasure "Keep everything": Held for the financial years, nothing mutated; enquiry-only customer Completed. Test: T_E1 | Held / Completed | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_E1 |
| T-R1b | Two reserves at once while TWO units of the type are free | Both 200 | PASS | Retry in a fresh scope on the exclusion constraint; 5 rounds per run, 3 of 3 runs all 200/200. |
| T-R1c | Two suspected-infectious flags at once while TWO isolation units are free | Both 204, two different isolation units | PASS | `BoardingTestGateTests.T_R1c_...`, 3 rounds per run, 3 of 3 runs PASS. |
| U-X4b | Allow-list: the only Billing seam a Boarding command handler injects is `IBillableChargeRecorder` (ADR-365, ratified) | No offender | PASS | `BoardingConventionTests.U_X4b_...` |
| A41 | Final invoice for a 3-night stay with a 900.00 deposit | Invoice net 3000.00, outstanding 3000.00, `allocate` offered | PASS | `BoardingTestGateTests.A41_...`. Billing does not auto-apply the earmarked deposit on issue: backlog row 61. |
| I-F3b | High incident recorded at 22:30 Dhaka | In the owner inbox after one delivery pass | PASS | `BoardingIncidentEndpointTests.I_F3b_...` |
| T-L4 | Rate limit: the three routes share one bucket per customer. Test: T_L4 | 4th call 429 | PASS | 1 test(s) passed in IntegrationTests (fresh DB): T_L4 |

## Mutation spot-checks (production file edited, tests run, file restored, `git status` clean)

A mutation is "caught" when at least one test fails because of it.

| ID | Mutation | Expected | Status | Evidence |
|---|---|---|---|---|
| M1 | Deposit earmark: drop the customer/branch filter in the deposit ledger | I_S1 fails | PASS | I_S1 and T_M6 FAILED under the mutation (caught); file restored, git status clean |
| M2 | Gate boundary "on or before" -> strictly before | U_C10b fails | PASS | U_C10b (dueOffsetFromDeparture 0) FAILED (caught); file restored, git status clean |
| M3 | Late check-out extends the allocation (window includes the overstay) | I_S7 / U_G24 fail | PASS | U_G24 FAILED (unit run); I_S7 and T_M4 FAILED (integration run) (caught); file restored, git status clean |
| M4 | Overstay flag handler does not flag | I_S10 fails | PASS | I_S10 and T_M4 FAILED (caught); file restored, git status clean |
| M5 | creditDue netting removed (`Net - CreditedNet` -> `Net`) | U_G22 / I_S11 fail | PASS | U_G22 FAILED (caught); U_G17 passed (it does not use CreditedNet); file restored, git status clean |
| M6 | Low-incident media filter removed from the owner diary | I_S3 fails | PASS | I_S3 FAILED (caught); file restored, git status clean |
| M7 | Vet-authority reason not required | U_G27 / I_S2 fail | PASS | U_G27 FAILED (caught); file restored, git status clean |
| M8 | Worker-disabled setting removed from the base factory and appsettings.Testing.json | A test fails (none is expected to — see Cases I could not run) | PASS | The new BoardingWorkerOffInOtherHostsTests FAILED (caught). No pre-existing test references the flag (grep); the whole suite was NOT re-run under this mutation, so "nothing else caught it" is from grep, not observation.; file restored, git status clean |
| M9 | Preview computes a different nights figure from the check-out | I_G2b fails | PASS | I_G2b and T_M3 FAILED (caught); file restored, git status clean |

## Acceptance-criteria coverage

Every handover section 18 row A1-A50. Filled in at the end against executed rows.

| Criterion | Covered by | Status |
|---|---|---|
| A1 units carry type/capacity/block; rate is the type's card | U-B1, U-B2, I-B1 | PASS |
| A2 unit under maintenance excluded | U-C3, I-B2, I-B3 | PASS |
| A3 types with prices and total | I-C1 (U-C5 has no test) | PASS |
| A4 bookings, maintenance, turnaround excluded | U-C2, I-C2, I-C12, T-R1 | PASS |
| A5 alternatives or waitlist when full | U-C7, I-C3, U-H1, I-H1 | PASS |
| A6 threshold applies weekly/monthly card with discount | U-B3..B6, I-C4, T-M2, T-M5b | PASS |
| A7 basis and discount itemised separately | U-B4, I-C4 | PASS |
| A8 overdue cat blocks; booking link returned | U-C10, U-C10b, U-C11, I-C5 | PASS |
| A9 after vaccination the same booking proceeds | I-C6 | PASS |
| A10 override: admin only, reason, audited | I-C7 (U-C12 override-survives untested) | PASS |
| A11 care profile with every FR-BRD-6 element required before reserving | I-C8 (U-C14 element validation untested) | PARTIAL |
| A12 each medication generates tasks | U-E1, U-E3 | PASS |
| A13 deposit computed, balance recorded | U-B9, I-D1, T-M1, T-M6 | PASS |
| A14 unpaid -> reserved, unit held only for the window | U-D2, I-D2, I-D4 | PASS |
| A15 deposit recorded -> confirmed (in-app only, NTF OQ-1) | U-A7, I-D3, I-D5, T-M6 | PASS |
| A16 window expires -> lapses, unit released | U-D3, I-D2 | PASS |
| A17 no online payment, no card or token stored | I-D7 | PASS |
| A18 multiple cats, one reservation | I-C9, U-E1 | PASS |
| A19 over capacity refused with the capacity | I-C10 | PASS |
| A20 check-in checklist | U-E11, I-E1 (U-E10 untested) | PASS |
| A21 incomplete checklist -> not checked_in | I-E1 | PASS |
| A22 daily tasks per unit assigned by roster | U-E1, U-E5, I-E2/E3 (I-E5 leave reassign untested) | PARTIAL |
| A23 extension generates tasks for added days | U-E4 only (I-E4 untested at HTTP) | PARTIAL |
| A24 completion records time and identity | U-E6, I-E6 | PASS |
| A25 overdue highlighted | I-E7 (grace minute-before untested) | PASS |
| A26 appetite/water/litter/mood/notes per cat | I-E8 | PASS |
| A27 two no-food entries -> not eating | U-E8, I-E8 | PASS |
| A28 media in the diary by date (references only) | I-F7, I-F8 | PASS |
| A29 diary viewable 12 months | I-F9 only (RetainUntil not asserted, U-F3) | PARTIAL |
| A30 incident severity/description/action | U-F1, I-F1, I-F2 | PASS |
| A31 medium/high notify; high within 15 min (in-app) | U-F1, I-F1; delivery-within-one-pass not asserted | PARTIAL |
| A32 unacknowledged high -> phone-call task | U-F2, I-F4, I-F5 | PASS |
| A33 referral links the stay (EMR side not built) | U-G4, I-G7 | PASS |
| A34 charges within the limit added | U-G1, U-G2, I-G8 | PASS |
| A35 above the limit: owner approval (EMR hold not built) | I-G9 | PASS |
| A36 extension updates stay, tasks and charges | T-M5, I-G4 (tasks part: I-E4 untested) | PARTIAL |
| A37 not free -> relocation offered, reason recorded | I-G5, I-G6 | PASS |
| A38 early check-out recalculated; difference credited | U-G6..G8, I-G2, T-M3, T-M3b (credit above zero is unreachable end to end, row 26) | PASS |
| A39 lost long-stay discount recalculated and shown | U-G6, T-M3c | PASS |
| A40 summary: logs, weights, incidents, meds, vet visits, belongings, signature | I-G12 asserts owner scope only; content untested | PARTIAL |
| A41 final invoice itemises deposit, nights, extras, vet charges | T-M2b, I-G14 (amounts yes; "deposit applied" line untested) | PARTIAL |
| A42 infectious cats only in isolation | U-E12, I-E9, I-E10, U-C16 | PASS |
| A43 isolation entries recorded | I-E9 | PASS |
| A44 dashboard shows every section | I-F10 (in-house, occupancy only) | PARTIAL |
| A45 dashboard read is side-effect free | no assertion | PARTIAL |
| A46 stays over one cycle invoiced per cycle | U-B8, I-G14, T-M2b | PASS |
| A47 final invoice for remaining nights only | T-M2b | PASS |
| A48 daycare hourly/day rates, no overnight unit | U-H2, I-H4, I-H5, I-H7 | PASS |
| A49 a cat cannot be in two overlapping stays | I-C13 | PASS |
| A50 task assignment follows rosters and leave | U-E5 (I-E5 untested) | PARTIAL |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| U-A2 | No test asserts one StatusHistory row per transition. U_X3 (is_active held Reserved..CheckedOut only) PASSED but is half the case. | Write the missing test (backlog rows 52 and 53). |
| U-A3 | No unit test. At HTTP I_D2 (lapse after the hold) and I_D4 (PendingApproval suspends the lapse) PASSED; the ReservationNotLapsable code before DepositDueBy is asserted nowhere. | Write the missing test (backlog rows 52 and 53). |
| U-A4 | No unit test for the 73 h / 71 h boundary. I_D6 PASSED for 5 days / 1 day out only. | Write the missing test (backlog rows 52 and 53). |
| U-A6 | No unit test. Two of three halves observed at HTTP: before arrival refused (CheckInBeforeTheArrivalDate, T_D2) and on arrival allowed (T_D1, T_D3); the day after arrival is tested nowhere. | Write the missing test (backlog rows 52 and 53). |
| U-C4 | No test of the deterministic choice among equal units. | Write the missing test (backlog rows 52 and 53). |
| U-C5 | No unit test; I_C1 PASSED (types with capacity >= cats) for 2 cats only, not the 3-cat / capacity 2 and 4 pair. | Write the missing test (backlog rows 52 and 53). |
| U-C6 | No test that an online request is never offered an isolation unit on the availability endpoint (ThePublicUnitTypes... PASSED for the public type list only). | Write the missing test (backlog rows 52 and 53). |
| U-C8 | No test that availability omits a type with no rate card. | Write the missing test (backlog rows 52 and 53). |
| U-C12 | "Override survives re-evaluation" is tested nowhere; I_C7 PASSED (override and audit only). | Write the missing test (backlog rows 52 and 53). |
| U-C14 | No validator or domain test for 7 feedings, duplicate time, medication without time. | Write the missing test (backlog rows 52 and 53). |
| U-D1 | No Application-level handler test exists (the build shipped none). I_D3 and I_D5 PASSED at HTTP but cannot show "enqueued before the save". | Write the missing test (backlog rows 52 and 53). |
| U-E7 | I_E7 PASSED for past due + grace; the minute-before side is not asserted. | Write the missing test (backlog rows 52 and 53). |
| U-E10 | No test of the belongings-photos setting true / false. | Write the missing test (backlog rows 52 and 53). |
| U-F3 | RetainUntil arithmetic is not asserted. I_F9 PASSED (purge source erases the file, keeps the incident photo). | Write the missing test (backlog rows 52 and 53). |
| U-F4 | Media type and size are validated by Files, not Boarding; only the caption bound is tested (U_F9 PASSED). | Write the missing test (backlog rows 52 and 53). |
| U-G9 | The RelocationReasonRequired refusal is not asserted; I_G6 PASSED for a relocation with a reason. | Write the missing test (backlog rows 52 and 53). |
| U-X2 | No test of the worker surviving a throwing pass (BoardingWorkCycle is a concrete class; a seam is needed). | A seam on `BoardingWorkCycle` (interface or virtual) so a throwing pass can be injected. |
| I-E4 | No HTTP test that an extension generates tasks for the added days (U_E4 PASSED for the generator; T_M5 for the money). | Write the missing test (backlog rows 52 and 53). |
| I-E5 | No test that approving leave reassigns pending tasks. | Write the missing test (backlog rows 52 and 53). |
| I-E11 | House-food issue is HO-IMP-001 (not merged into this branch); only the in-memory StayReferenceReader fake is tested (InMemoryBoardingStayReferenceReaderTests PASSED). | HO-IMP-001 merged into this branch (then the stay-reference check can be driven end to end). |
| I-F11 | No performance test with 60 units / 50 stays / 500 tasks exists; not built by the gate (would need a bulk seeder). | A bulk seeder (60 units, 50 stays, 500 tasks) and a timing harness. |
| I-G15 | Customer merge moving STAYS to the survivor has no HTTP test (U_H9 PASSED for waitlist and daycare rows only). | Write the missing test (backlog rows 52 and 53). |
| R15 | No behavioural test of an ended stay older than 90 days. GetRefundableAsync was READ and has no date filter; that is reasoning, not observation. | Write the missing test (backlog rows 52 and 53). |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| Credit above zero after an early check-out, end to end | An invoice needs 30 elapsed nights, so the invoiced month always still applies; unit-covered only (U_G22, U_G23); backlog row 26 | BRD build |
| Payment-versus-lapse race, "Cancelled and refundable" branch | The payment won all 8 rounds; a deterministic interleave needs a seam (row 56) | BRD build |
| Whole-suite run under mutation M8 | 12 minutes per mutation; the new test catches it and grep found no other reference | Coordinator, if wanted |
| Email/SMS external-provider delivery, object storage uploads | Not built (NTF OQ-1, FIL OQ-1); in-app only | NTF / FIL owners |
| EMR's side of referral/approval, Billing Phase J charges carrying a stay id | Not merged (rows 39, 40) | Integrator |
| Dashboard p95, I-F11 | No harness; cost is high (row 53) | BRD / RPT |
| Cross-test interference between Boarding rows and Reporting's delivery passes | Unverified suspicion only (row 58) | Coordinator |

## Run record: race fixes and ACCEPTANCE follow-up (2026-10-15)

Database `catecommerce_brd` (full connection string in `CATECOMMERCE_TEST_DB`), build output `bin/DebugFix/`.

| Run | Filter | Result |
|---|---|---|
| Race 1 | `T_R1b`, `T_R1c`, `I_H3b` | 3 passed, 0 failed |
| Race 2 | same | 3 passed, 0 failed |
| Race 3 | same | 3 passed, 0 failed |
| Boarding integration | `FullyQualifiedName~Boarding` | 129 passed, 0 failed, 0 skipped (15 m 23 s) |
| Boarding domain | `FullyQualifiedName~Boarding` | 99 passed, 0 failed |
| Boarding application | `FullyQualifiedName~Boarding` | 3 passed (U_X4, U_X4b, U_X5) |

I-C12 (twenty parallel reserves for the last unit) is in the Boarding integration run: it passed, one winner and nineteen 409, now through the exclusion constraint itself because nothing serialises the callers.
