GATE: ACCEPTANCE
HANDOVER: HO-BRD-001
COMMITS: 5a187b6b..12ec6b88 on ho/BRD-001 (build 2d5f1e38..95a97bab; gate-fix 5d6f16fc, fc6ef889, cede66fe; TEST commits af3d9a56..1043e597; race fix 26aa5f94; hardening 12ec6b88)
DATE: 2026-10-15
STATUS: FAIL

One HIGH finding fails this gate. A second HIGH is a missing test run, not a code defect.
- **HIGH 1.** `BoardingClaimGate` is an in-process application lock on unit, isolation and daycare-slot claims. §4.4 forbids exactly that ("No application lock, advisory lock or `SELECT … FOR UPDATE` for occupancy"). The builder brought it in and recorded it in its own ADR-352 amendment and §1.1. No owner or coordinator authorised it, and no earlier gate checked it against §4.4.
- **HIGH 2.** Nobody has run any test against the hardening commit 12ec6b88, and this gate could not run one either. The reserve handler was restructured in that commit.

All 50 criteria have evidence. Four have an upstream half that depends on another area: A33, A34, A35 and A41. Those halves are recorded and are not counted here as met.

## Prior gates (from docs/gates and docs/test-cases, not from commit messages)

| Gate | Record | Status |
|---|---|---|
| ARCHITECTURE | HO-BRD-001-ARCHITECTURE.md: first pass 43437fb8 FAIL; combined re-check 99a982d8 | PASS |
| SECURITY | HO-BRD-001-SECURITY.md (0d6d3b90) | PASS |
| CODE_REVIEW | HO-BRD-001-CODE_REVIEW.md: dc8e7a68 FAIL (HIGH: worker on in other hosts). The HIGH was closed in the combined re-check 99a982d8, recorded in the ARCHITECTURE file. There is no separate CODE_REVIEW re-check file. | PASS (via the combined re-check) |
| TEST | HO-BRD-001-TEST.md (4b6c043b, 1043e597) and TC-HO-BRD-001.md: 212 rows, 187 PASS, 2 FAIL, 23 BLOCKED, 0 NOT RUN | PASS |
| Race-fix review | HO-BRD-001-RACEFIX-COMBINED_REVIEW.md (7086b208), for 26aa5f94 | PASS |
| Hardening 12ec6b88 | **No record.** Under the gate-scope rule a follow-up this size needs "just a test", and that test has not been run (see HIGH 2). | **missing** |

## Runs requested by the coordinator: NOT EXECUTED

- Build: `dotnet build CatEcommerce.sln` at 12ec6b88 is green (0 errors, 41 analyser warnings).
- T-R1b, T-R1c, I-H3b and the filtered Boarding integration classes were **not run.** `CATECOMMERCE_TEST_DB` is not set in this shell. The local Postgres needs a password (`No password has been provided but the backend requires one (SCRAM-SHA-256)`), and this gate is not permitted to read it from the developer's settings file.
- A first attempt with an empty password failed with 3/3 connection errors before any assertion ran. That is not a test result.
- No run evidence for 12ec6b88 therefore exists anywhere. See HIGH 2.

## FINDINGS

- **[HIGH] 1.** `src/Application/CatEcommerce.Application/CommandHandlers/Boarding/BoardingClaimGate.cs:13-31`. Call sites:
  - `BoardingStayCommandHandlers.cs:239-242` (reserve)
  - `BoardingCareCommandHandlers.cs:252` (suspected-infectious flag)
  - `BoardingWaitlistDaycareCommandHandlers.cs:185` (daycare)
  - `BoardingLifecycleCommandHandlers.cs:38` (confirm)

  **Rules broken.** §4.4: "No application lock, advisory lock or `SELECT … FOR UPDATE` for occupancy -- the exclusion constraints are the control (ADR-219)". §20.4 points the same way ("do not replace the constraints with an application lock").

  **What the code does.** A static striped `SemaphoreSlim` serialises, per process, the read-pick-save of every unit, isolation and daycare-slot claim. The wait is bounded to 10 s, after which the caller gets `409 BoardingBusy`.

  **How it entered.** Backlog rows 49 and 50 proposed a retry on 23P01 or the unique violation. The race fix (26aa5f94) chose the lock instead. The builder then rewrote ADR-352 (12ec6b88, `.ai/decisions/adr.md`) and added the "Concurrency" paragraph to §1.1. It is declared, but nobody with authority approved it:
  - it is not among the owner decisions;
  - it is not among the coordinator ratifications;
  - the race-fix review ruled on its mechanics and never cited §4.4.

  **Consequence beyond the letter.** Inside one process, I-C12 now proves the lock, not `ex_stay_allocation_unit`. The 20 parallel reserves queue on one key and never reach the constraint. The constraint is still in place and was proven by the TEST gate's I-C12 run at cede66fe, before the lock existed. Today no test at HEAD would catch a regression of the constraint.

  **To close, either:**
  - (a) The owner or coordinator ratifies the departure in §1 Amended. The ratification must:
    - state that the lock is a fairness device and not the occupancy control;
    - record the single-instance deployment assumption;
    - add one test that drives two claims for the same unit through **different** gate keys, or through a second host, so that the exclusion constraint itself is shown to refuse the loser.

    This is the recommended route. The constraint stays the control, so §4.4's stated reason holds.
  - (b) Replace the lock with the backlog's proposed remedy: after a 23P01 or unique violation, retry the claim in a fresh scope with the next free unit or slot.

  The confirm key (`confirm:<stay>`) is a status race, not occupancy, and is outside §4.4.

- **[HIGH] 2.** 12ec6b88 (`BoardingStayCommandHandlers.cs`, `BoardingClaimGate.cs`, `BoardingCareCommandHandlers.cs`, `BoardingControllerBase.cs`) has **no test run on record.** The commit changes the following, and no gate, builder note or TC row records a run after it:
  - the reserve handler now loads the stay before taking the gate;
  - the reserve key is now per branch and unit type;
  - isolation flags now share a key with Isolation reserves;
  - `BoardingBusy` now maps to 409;
  - T-R1c is new.

  To close, run on `catecommerce_brd` with the full connection string:
  - `--filter "FullyQualifiedName~T_R1b|FullyQualifiedName~T_R1c|FullyQualifiedName~I_H3b"`, 3 runs;
  - `--filter "FullyQualifiedName~Boarding"`.

  Record the counts in TC-HO-BRD-001.md: flip T-R1b and I-H3b to PASS and add T-R1c.

- [MEDIUM] 3. docs/handover/HO-BRD-001.md §4.4 / ADR-232 versus ADR-365 is backlog row 4, open for the coordinator. **Until it is ratified, §4.4 read literally forbids the push model**: `IBillableChargeRecorder` enlists a Billing `ChargeAggregate` in Boarding's save. This becomes a HIGH if it reaches merge unratified. Recommendation below.

- [MEDIUM] 4. Several criteria rest partly on source this gate read, not on a test. None lacks evidence; tests would harden them. The gaps:
  - A20: U-E10, the photos setting.
  - A23 and A36: I-E4, an extension generating tasks.
  - A31: the handover's I-F3, a High incident at 22:30 Dhaka delivered in-app in one notification pass.
  - A40: summary incidents, medications, vet visits, belongings and signer.
  - A41: the deposit applied on the final invoice.
  - A44: the dashboard's `arrivals`, `departures`, `overdueTasks` and `unpaidBalances`.
  - A50: I-E5, leave reassigning tasks.

  One backlog row: "ACCEPTANCE: turn source-read evidence into tests", listing those test ids.

- [LOW] 5. `BoardingClaimGate.EnterAsync(key, ct)` is called without a token at all four call sites. The XML doc says "Cancellation by the caller propagates", and the race-fix review asked for the request token. A disconnected caller still waits up to 10 s. Undeclared.
- [LOW] 6. `BoardingIncidentQueryHandlers.cs` (`GetBoardingDashboardQueryHandler`): pending tasks are `Take(500)` before the overdue filter, so the dashboard's overdue list is silently truncated at a busy branch. This is the same class as resolved row 33.
- [LOW] 7. `docs/test-cases/TC-HO-BRD-001.md` is stale against HEAD:
  - T-R1b and I-H3b still read FAIL and "Skip", but the Skip is gone (`BoardingTestGateTests.cs:524,686`);
  - T-R1c is not listed;
  - the A40 roll-up says "content untested", but I_G12 asserts weightIn, weightOut, the observation notes and the charge count.
- [LOW] 8. Cross-area backlogs on master do not yet know what Boarding delivers. To be done at merge:
  - XCT rows 2 (`boarding-media` source) and 3 (eraser and review source) are now met by this branch;
  - XCT row 21 (scope reconciliation "when a second personal-data context ships") is now triggered;
  - EMR and IMP carry no row for E1-E4 and I1. BRD rows 40 and 41 are the only record.

## CRITERIA

Test names are the executables named in TC-HO-BRD-001.md. Statuses are as observed by the TEST gate, at cede66fe plus test commits, unless marked otherwise. "Source" means code this gate read.

- A1 MET. RateCards_AreAppendOnly (U-B1, U-B2 at HTTP), I_B1 PASS. ADR-221 interpretation recorded: the per-type rate, OQ-4. §1.1 mismatch 1 also makes cards per branch.
- A2 MET. I_B2 and I_B3 PASS.
- A3 MET. I_C1 PASS: 1 cat gives Standard and Deluxe; 2 cats gives Deluxe only; total 5000.00. U-C5 is BLOCKED, but I_C1 already shows the capacity filter.
- A4 MET. U_C2 PASS, I_C2 PASS, I_B2 PASS, and I_C12 PASS before the lock (one 200 and nineteen 409 UnitNoLongerAvailable). See HIGH 1 on what I_C12 proves now.
- A5 MET. I_C3, U_H1..H3 and I_H1/I_H2 PASS.
- A6 MET. U_B3..U_B6, I_C4, T_M2 (31 nights, Monthly, 22500) and T_M5b (15 nights, Weekly, 13000) PASS.
- A7 MET. U_B4 and I_C4 PASS: discount lines are separate.
- A8 MET. U_C10, U_C10b (owner "On or before"), U_C11 and I_C5 PASS (`VaccinationGateBlocked` with `bookingLink`).
- A9 MET. I_C6 PASS (200 Reserved, profile notes unchanged).
- A10 MET. I_C7 PASS (Administrator only, 403 for Reception, BusinessEvent audit with the reason). U-C12 (an override surviving re-evaluation) is outside the criterion text.
- A11 MET. I_C8 PASS: `CareProfileIncomplete` names Lulu, not Mitu. Every reserve path sends `ProfileBody` (BoardingIntegrationTestBase.cs:238-247) carrying every FR-BRD-6 element, and gets 204:
  - feedings with time, portion and food source;
  - medications with times;
  - litter;
  - vet authorisation and limit;
  - emergency contact;
  - notes.

  The elements are consumed: medications by I_E2_E3, the limit by I_G8. U-C14 (the bounds) is a gap and not part of the criterion. **TEST's PARTIAL is overruled.**
- A12 MET. U_E1 (nine tasks), U_E3 and I_E2_E3 PASS (a `Medication` kind in the list).
- A13 MET. U_B9, I_D1, T_M1 (deposit 900.00) and T_M6 (balance 2100.00) PASS.
- A14 MET. I_D2 and I_D4 PASS. U-A3 is BLOCKED: the code before DepositDueBy is not asserted, but the lapse window itself is.
- A15 MET. T_M6 (899.99 refused, 900.00 confirms), I_D3 and I_D5 PASS. Caveat: in-app only until a provider exists (HO-NTF-001 OQ-1).
- A16 MET. I_D2 PASS: Cancelled/ReservationLapsed, unit available, lapse message.
- A17 MET. I_D7 PASS (no card or token column). No gateway type is in the diff (ARCHITECTURE sweep).
- A18 MET. I_C9 PASS (three cats, one stay, three profiles, 4200.00) and U_E1.
- A19 MET. I_C10 PASS (`UnitCapacityExceeded` with the capacity).
- A20 MET. I_E1 PASS lists "weight" and "owner signature". U_E11 PASS covers the signature bounds. Source `BoardingStayAggregate.Care.cs:61-71` lists identity, vaccination proof, weight, arrival health, belongings, belongings photos (default on) and signature, as §8.11 requires. U-E10 is a gap (MEDIUM 4).
- A21 MET. I_E1 PASS: the stay stays Confirmed after a 400.
- A22 MET. I_E2_E3 PASS:
  - grouped by unit;
  - Feed, Medication and LitterChange tomorrow;
  - **every task `assignedCaretakerId` = the rostered caretaker**.

  U_E1 covers the nine kinds and due times, including enrichment and observation. U_E5 PASS covers round-robin and no-shift-unassigned. I-E5 belongs to A50. **TEST's PARTIAL is overruled.**
- A23 MET by composition:
  - U_E4 PASS: the generator emits tasks for dates added to the stay;
  - I_E2_E3 PASS: the worker generates from the stay's current dates;
  - I_G4 and T_M5 PASS: an extension moves `DepartureOn`.

  No single test drives all three (I-E4, MEDIUM 4). **PARTIAL overruled; residual gap recorded.**
- A24 MET. I_E6 PASS: an empty body gives 204, `completedBy` is the caller, `completedOn` is set, and a second tap is harmless.
- A25 MET. I_E7 PASS (`isOverdue` after due + grace). Source: the dashboard `overdue` uses the same `CareTaskAggregate.IsOverdue`.
- A26 MET. I_E8 and I_G12 PASS (portion, water, litter, mood and notes recorded and read back).
- A27 MET. U_E8 and I_E8 PASS.
- A28 MET. I_F7 PASS (diary by date, a hidden photo disappears) and I_F8 PASS. Real stored files now exist (README C-5), so the reference-only caveat is lifted.
- A29 MET:
  - I_F9 PASS: past the cut-off the plain item is purged and the incident photo kept;
  - OperationsConventionTests asserts the `retention.boardingMediaMonths` default is "12";
  - source `RetentionWorkCycle.SourceCutoff` gives the cut-off as `now.AddMonths(-boardingMediaMonths)`.

  U-F3 (RetainUntil arithmetic) is redundant: XCT's cut-off is the rule (§1.1 mismatch 5). **PARTIAL overruled.**
- A30 MET. U_F1, I_F1 and I_F2 PASS.
- A31 MET by composition:
  - I_F1 PASS: Medium and High are enqueued by the command (outbox rows `boarding.incident-medium` and `-high`), Low is not;
  - U_A8 PASS: High is Urgent, Medium is Normal;
  - NTF's merged QuietHoursEndpointTests.UrgentTemplate_At2230_SentAtOnce and InboxCopy_At2230_DeliveredAtOnce passed in TEST's full run (1916 passed, these not among the 4 failures).

  The handover's Boarding-specific I-F3 is not run (MEDIUM 4). The caveat "proved for the in-app copy" stands. **PARTIAL overruled; residual gap recorded.**
- A32 MET. U_F4..U_F6, I_F4 (escalated after 31 min, on `callOwner`, cleared by the phone call) and I_F5 PASS.
- A33 MET for this slice; the EMR half is DEPENDENCY-BLOCKED:
  - I_G7 PASS: the referral is stored, the cat is away, `CatNotPresent`;
  - the consultation opened by HO-EMR-001 (E1) is not built (BRD row 40, §1.1 (b)).
- A34 MET for this slice; the invoice half is DEPENDENCY-BLOCKED:
  - U_G1, U_G3 and I_G8 PASS: headroom, and a commitment once per charge;
  - "added to the stay invoice" needs Phase J sources carrying `BoardingStayId` (BRD 39, BIL 76/103).
- A35 MET for this slice; the hold is DEPENDENCY-BLOCKED:
  - U_G7 and I_G9 PASS: the approval is requested and the owner answers;
  - the treatment hold is HO-EMR-001 E2/E4 (row 40).
- A36 MET by composition:
  - I_G4 PASS: the stay is updated and a stale version gets 409;
  - T_M5 and T_M5b PASS: charges 8000 then 13000;
  - tasks: as A23.

  **PARTIAL overruled; I-E4 gap recorded.**
- A37 MET. I_G5 and I_S9 PASS (the refused extension names the free unit) and I_G6 PASS (relocation with a reason). U-G9 is a gap.
- A38 MET. U_G14, U_G17, U_G22 and U_G23, plus I_G2, T_M3 and T_M3b PASS. Credit above zero is unreachable end to end (row 26) and is pinned at planner level. The refund or credit itself is HO-BIL-001's.
- A39 MET. U_G14/U_G18 and T_M3c PASS (10 nights: discount gone, 10000).
- A40 MET:
  - I_G12 PASS asserts weightIn 4.2 and weightOut 4.4, the observation note and the charges (the TEST roll-up understated it);
  - source `GetStaySummaryQueryHandler` (BoardingDepartureQueryHandlers.cs:38-61) projects incidents (Low hidden from the owner), completed medication doses, referrals as vet visits, belongings with return state, and the check-out signer and time.

  Residual assertions are MEDIUM 4. **PARTIAL overruled.**
- A41 MET for this slice; the rest is DEPENDENCY-BLOCKED:
  - T_M2 PASS: cycle invoice 21600, final 900, sum 22500;
  - I_G14 PASS: one invoice per cycle, the final invoice for the rest;
  - extras (AdditionalCat) are charged per T_M4;
  - vet-charge lines need Phase J and EMR (rows 39, 40);
  - deposit application is Billing's earmark matching and is not asserted on a Boarding invoice (MEDIUM 4).

  §18 itself says "rendered by HO-BIL-001 (P2)". **Recorded, not passed silently.**
- A42 MET. I_E9 and I_E10 PASS (`IsolationUnitRequired`). U-C16 is covered at HTTP.
- A43 MET. I_E9 PASS: entries listed with the staff member.
- A44 MET:
  - I_F10 PASS: occupancy;
  - I_F4/I_F5 PASS: `callOwner`, `openIncidents`;
  - I_S5 PASS: `refundDue`;
  - I_S10/T_M4 PASS: `attention`;
  - source `GetBoardingDashboardQueryHandler` builds `arrivals` (Confirmed, arriving today), `departures` (in house, leaving today), `notEating` (`NotEatingRule`), `overdue` (`IsOverdue`) and `unpaid` (deposit awaited plus invoice outstanding).

  The p95 (I-F8/I-F11) is not in the criterion text. **PARTIAL overruled; residual assertions are MEDIUM 4.**
- A45 MET for the server half. Source: the dashboard handler is in `CatEcommerce.Read` and reads with `AsNoTracking`. The only `.Add` calls are to in-memory result lists, and there is no `SaveChanges` or dispatch. Read does not reference Application (ARCHITECTURE). The 30 s refresh is HO-BRD-002. **PARTIAL overruled.**
- A46 MET. U_B8, I_G14 and T_M2 PASS.
- A47 MET. T_M2 PASS: the final invoice is 900.00, the night after the cycle.
- A48 MET. U_H4 (hourly capped at the day rate), I_H4, I_H5 and I_H7 PASS. Source: `DaycareVisitAggregate` never allocates a unit (ADR-367). The concurrency case I-H3b failed at TEST and passed in the race-fix review at 26aa5f94; it is unrun at 12ec6b88 (HIGH 2).
- A49 MET. I_C13 PASS (`CatAlreadyBoarding` across two branches).
- A50 MET:
  - U_E5 PASS: rosters, and its last assertion excludes a caretaker (the leave path);
  - source `ReassignCareTasksOnLeaveApprovedHandler` (BoardingCareEventHandlers.cs:93-113) re-runs the assigner without the caretaker on leave for pending tasks in range. It is registered on `StaffLeaveApprovedEvent` (BoardingModule.cs:236).

  No test drives the event end to end (I-E5, MEDIUM 4). **PARTIAL overruled; residual gap recorded.**

### Ruling on the 11 PARTIAL rows
All 11 have concrete evidence: A11, A22, A23, A29, A31, A36, A40, A41, A44, A45, A50. A11, A22, A29, A40 and A45 are fully met. A23, A31, A36, A44 and A50 are met by composition, with the gap a single named test (MEDIUM 4). For A41, the Boarding half is met and the other half is upstream and recorded. None is a BLOCKER or HIGH.

### Ruling on the 23 BLOCKED cases
No BLOCKED case leaves a criterion without evidence. Each one either sits outside the criterion text, or is covered by another executed test or by source:

| Cases | Where they fall |
|---|---|
| U-A2, U-A3, U-A4, U-A6, U-C4, U-C6, U-C8, U-C12, U-D1, U-E7, U-F4, U-G9, U-X2, R15 | Outside the criterion text |
| U-C5, U-C14, U-E10, U-F3 | Covered by another test or by source |
| I-E4, I-E5 | Composition (A23, A36, A50) |
| I-F11 | NFR, not a criterion |
| I-G15 | The merge contract, §3.1 "also delivered", not a §18 row |
| I-E11 | HO-IMP-001, not a §18 row |

The tests that would turn composition into direct evidence: I-E4, I-E5, U-E10, U-C14, the handover's I-F3, a summary-content assertion, the dashboard section assertions, and a deposit-applied final-invoice case (MEDIUM 4). Backlog rows 52 and 53 already hold the rest.

## Deferred cross-area items

| Item | Authorised? | Blocked on | Recorded where |
|---|---|---|---|
| R24-R26 and the 3 boarding tiles | Yes: §4.2 places the reports with HO-RPT-001; RPT owner decision 2026-10-13 ("not delivered yet, never faked") | Boarding merging (RPT is merged) | BRD row 38, §1.1 (d), RPT row 3. Complete. |
| XCT `boardingTasks` snapshot (`BoardingTaskSnapshotSource`, `BoardingTasks.View`) | Partly. §4.2 gives the offline list to HO-XCT-001; this slice delivers the day-task read. XCT row 1 says the source "belongs with HO-BRD-001 / C-BRD-1"; BRD row 37 says it is not built here. **Ownership ping-pong, as in C-7.** | Owner/coordinator must name the builder | BRD 37, XCT 1. **Owner question.** |
| Phase J `BoardingStayId` on clinic charges | Yes: BIL owner decision 3 "skip until Boarding"; §20.2 | EMR/SRG adding `BoardingStayId` to events and visit/surgery, plus Phase J's three sources setting the billing group. §14 forbids Boarding doing it. | BRD 39, BIL 76/103, master §1 Amended row (Phase J merge note). Complete. |
| EMR referral events (E1-E4) | Yes: §4.2/§12.8 defer them to HO-EMR-001 | An EMR follow-up (EMR is merged without them) | BRD 40, §1.1 (b). **Not in EMR's backlog** (LOW 8). |
| IMP house-food validation (I1) | Yes: §4.2/§12.8 | An IMP follow-up: the stock-issue path must call `IBoardingStayReferenceReader.IsInHouse` | BRD 41, §1.1 (c). **Not in IMP's backlog** (LOW 8). I-E11 is BLOCKED correctly. |

## ADR-365 versus ADR-232 / §4.4: recommendation to the coordinator

**Ratify ADR-365**, narrowly, in the coordinator's own §1 Amended row. Reasons:
1. The §12.8 pull contract (P2/P3) does not exist. Merged Billing is push-only:
   - CONTRACTS row 10 and HO-BIL-001 ADR-178 name HO-BRD-001 as a consumer of `IBillableChargeRecorder`;
   - §20.2 forbids shimming a different contract, so using the published seam is the compliant response.
2. The literal alternative is worse. A Billing event handler recording the charge after Boarding's commit would lose atomicity: a charge is dropped if the handler fails (`.ai/domain-events-and-outbox.md` §3).
3. ORD-001 is the precedent (`OrderBilling`).

**Wording.** "§4.4/ADR-232 are read as: no Boarding command handler holds a repository of a Billing aggregate. Charges are recorded only through `IBillableChargeRecorder`, enlisted in Boarding's own save, and invoices only through `IssueInvoiceFromChargesCommand` dispatched by the worker."

**Condition.** Extend `BoardingConventionTests.U_X4` with an allow-list, so that the only Billing-namespace dependencies of Boarding command handlers are those two seams. Today the test checks repositories only, so a new seam would pass unseen.

## Merge checklist (integrator)

1. **Migrations.** Regenerate all **seven** on master:
   - AddBoardingUnits, Stays, Care, IncidentsAndMedia, Charges, WaitlistAndDaycare, GateFixes. §13 said six; the seventh is accepted (row 42).
   - Master now carries Phase J's `20261014033110_AddVisitBillingFacts` and its snapshot. Rebuild the snapshot and re-check `has-pending-model-changes`.
   - Keep the hand-written SQL: the 3 exclusion constraints, `btree_gist`, and the literal `RetentionGuardSql.AttachTo` per Forbidden table.
2. **Flow doc.** Apply the §1.1 "Built -- HO-BRD-001" text to `.ai/flows/boarding-stay-flow.md` under the coordinator's authority (row 2/A1). Add a line on the claim gate if HIGH 1 is ratified.
3. **Backlog numbering.** BRD-001 rows 1-60 and BRD-002 rows 45-75 (in HO-BRD-002's gate files) collide on 45-60. Merge into one numbering and update the cross-references:
   - gate files;
   - §1.1;
   - TC rows;
   - BIL 103.
4. **Commit trailers are mixed.**
   - Sonnet: 9eb583c7, 16909328, 7df0573e, 7ddf9852, 95a97bab, 26aa5f94, 12ec6b88.
   - Opus 5.5 (1M context): all the others.

   The builder blames a harness reminder. Decide whether to normalise this in the merge commit message; history is not rewritten.
5. **roles.sql.** Re-apply or keep the `'boarding'` token (ratified, row 7). Master's `deploy/database/roles.sql` is unchanged since 5a187b6b, so no conflict is expected.
6. **Shared docs.** `docs/handover/HO-BRD-001.md` §1 is amended on both sides: master has the Phase J note row, the branch has §1.1. Keep both. `CONTRACTS.md` and `adr.md` changed on both sides; there is no number collision (master tops out at ADR-346; BRD uses 350-369 and CONTRACTS 50-59).
7. **Mirrors.**
   - HO-FIL-001 §1 gets the `FilePurposeAccess` mapping (row 6).
   - HO-BRD-002 §1 gets the referral-return route and `isLate` (rows 14, 47).
   - XCT rows 2, 3 and 21 (LOW 8).
   - EMR and IMP get rows for E1-E4 and I1 (LOW 8).

## PROPOSED ROWS for docs/backlog/BRD.md (prefix "ACCEPTANCE:"; the integrator numbers them)
- HIGH 1: `BoardingClaimGate` versus §4.4. Ratify, with a constraint-path test through different keys or a second host, or replace with retry-next-free.
- HIGH 2: run T-R1b, T-R1c, I-H3b (3 runs) and `~Boarding` at 12ec6b88; update TC-HO-BRD-001.md.
- MEDIUM 3: ADR-365 ratification (amends row 4). It becomes HIGH if unratified at merge. Add the U_X4 allow-list.
- MEDIUM 4: tests for composition evidence: I-E4, I-E5, U-E10, the handover's I-F3, the summary content, the dashboard arrivals/departures/overdue/unpaid, and the deposit applied on the final invoice.
- LOW 5: pass the request `CancellationToken` into `BoardingClaimGate.EnterAsync`.
- LOW 6: the dashboard overdue list is truncated by `Take(500)` before the filter.
- LOW 7: refresh TC-HO-BRD-001.md (T-R1b, I-H3b, T-R1c, the A40 roll-up).
- LOW 8: mirror rows in XCT (2, 3, 21), EMR (E1-E4) and IMP (I1).
- OWNER: who builds `BoardingTaskSnapshotSource` (XCT 1 versus BRD 37).

## SCOPE CHECK
- files in allowed scope: 225/225 (5a187b6b..12ec6b88). The ARCHITECTURE re-check counted 62/62 for the fix range and accepted the earlier 4 out-of-scope files, after the `.ai/flows` revert and the ratification of 3 of them. Every file changed after fc6ef889 is a Boarding source file, a Boarding test, `docs/backlog/BRD.md`, `docs/gates/HO-BRD-001-*`, `docs/test-cases/TC-HO-BRD-001.md`, §1.1 of the handover, or the ADR-350..369 block of `.ai/decisions/adr.md`.
  - 12ec6b88 edits ADR-352 in place. That text was appended by this branch, so it is still append-only against master.
  - `.ai/flows/**` has no net change.
- files outside allowed scope: none.
- §4 out-of-scope work found: **`BoardingClaimGate`, an application lock for occupancy (§4.4), HIGH 1.** Also the push-model write through Billing's seam (§4.4 / ADR-232), which is declared and pending coordinator ratification (MEDIUM 3). Nothing else: no gateway, card or token field, upload endpoint, stored availability, stored overdue or not-eating flag, enqueue from an event handler, `[Authorize]`, 202 or time-zone library.
- UNDECLARED DEVIATIONS:
  - LOW 5: the doc comment promises cancellation that no caller passes.
  - The lock itself was declared, but **without naming the §4.4 rule it departs from**. Its review therefore never weighed that rule.

---

## Re-check -- 2026-10-15 (range 45bac6da..84caeffd)

GATE: ACCEPTANCE (re-check)
HANDOVER: HO-BRD-001
COMMITS: 84caeffd (ho/BRD-001)
DATE: 2026-10-15
STATUS: PASS

**Correction note.** Commit 283e01a9 wrongly appended unrelated HO-XCT-002 re-check text (a stale scratch file) under this file. The commit that carries this section removes it. That text was never a verdict on HO-BRD-001.

Inputs:
- **Coordinator rulings:**
  - HIGH 1 is to be fixed by a fresh-scope retry.
  - ADR-365 is ratified narrowly, with the coordinator's own §1 row and U_X4b.
  - `BoardingTaskSnapshotSource` goes to BRD-001-FU1 (row 63).
  - `BoardingStayId` on clinic charges goes to BIL-001-FU2 (row 67).
  - Mirror rows: EMR 69, IMP 63.
- **Owner decisions, 2026-10-15:**
  - "Measure at merge": the p95 moves to the integrator's real-API run.
  - "Ship, follow-up fixes": owner phone, deposit outcome and amount, and `canOverrideGate`.
  - "Apply automatically": the final-invoice deposit, a Billing follow-up within BIL-001-FU2.

**Evidence basis, stated plainly.** This gate did NOT execute any test in this re-check either:
- `CATECOMMERCE_TEST_DB` is still not set for this gate.
- The builder wrote the full connection string, password included, into `docs/test-cases/TC-HO-BRD-001.md`. This gate did not use it, because reading that credential had already been refused to it (LOW R3).
- The mutation in step 4 was therefore not run.

The verdict rests on three things:
- (a) static review of the diff;
- (b) the TEST gate's **observed** runs at cede66fe, whose claim bodies are identical to the `AttemptAsync` bodies at 84caeffd (item 4);
- (c) the builder's recorded run in TC-HO-BRD-001.md: race tests 3 runs x 3 passed, `~Boarding` integration 129/0/0, domain 99/0, application 3/0. **This is builder evidence, not gate-observed.** The coordinator directed this gate to rely on it. The integrator's merge-time run is the first independent observation of the retry path.

### What was verified

1. **No application lock is left.**
   - `BoardingClaimGate.cs` is deleted, and so are the `BoardingBusy` code and message and the 409 mapping.
   - In Boarding code, `SemaphoreSlim`, `lock (`, `Monitor.`, `Mutex`, `ReaderWriterLock`, `Interlocked`, `pg_advisory` and `FOR UPDATE` find nothing.
   - The remaining `lock` hits in `src/**` are pre-existing Inventory and Pharmacy worker state; the advisory lock is Appointment's own.
   - ADR-352 and §1.1 "Concurrency" now state §4.4 compliance. Both are branch-appended text, so the change is still append-only against master.
   - **HIGH 1 is closed.**
2. **The retry is bounded and uses a fresh scope** (`ClaimRetry.cs`):
   - `MaxAttempts = 4`. Attempt 1 runs in the request scope. Attempts 2 to 4 each run in `IFreshScopeExecutor.ExecuteAsync`, which is `IServiceScopeFactory.CreateAsyncScope()` plus a new handler and a new DbContext, disposed after use.
   - It retries only when `lost(ex)` is true:
     - for a claim, `IsOccupancyClaimLost` = 23P01, 40P01, or 23505 on `ix_daycare_slot`;
     - for Confirm, `IsConcurrencyConflict` = `DbUpdateConcurrencyException`.
   - Every other exception propagates at once.
   - When the attempts run out, a claim returns the honest violation (`UnitNoLongerAvailable` or `DaycareFull`), and Confirm rethrows to `ConcurrencyExceptionFilter`, giving 409.
   - The attempt bodies catch only `DomainRuleViolationException`, so the 23P01 or 23505 from `SaveAsync`/`UpdateAsync` reaches the retry.
   - A retried `ex_stay_cat` loss re-reads and ends in the pre-check's `CatAlreadyBoarding` (I_C13's contract).
3. **Application has no Npgsql or EF reference.**
   - No `using Npgsql` or `using Microsoft.EntityFrameworkCore` appears under `src/Application`, and the Application csproj names neither package.
   - The ports are in Domain/DomainServices. `FreshScopeExecutor` and `PostgresUniqueViolationDetector` are in Infrastructure/Boarding, registered as Singletons. Both are stateless; the executor depends only on the singleton `IServiceScopeFactory`, so this is `ValidateScopes`-safe.
4. **Would T-R1b, I-H3b and T-R1c fail with a single attempt?** Not executed; equivalent evidence instead:
   - `git diff cede66fe 84caeffd` on the four handlers changes only the constructor parameters and the `HandleAsync -> ClaimRetry.RunAsync(..., AttemptAsync)` wrapper. Each `AttemptAsync` body is the cede66fe body.
   - `MaxAttempts = 1` would therefore be exactly the code the TEST gate observed failing:
     - T-R1b gave 200/409 in 5 of 5 rounds in each of 3 runs;
     - I-H3b gave one 201 and two 409 in 3 of 3 runs.
   - **T-R1c has no observed single-attempt evidence**: it was written after cede66fe. Statically, two parallel flags pick the same first free isolation unit, and the loser's 23P01 would surface as 409.
   - I-C12 again reaches `ex_stay_allocation_unit`, because nothing serialises the 20 callers. The losers' retries find no free unit and return 409 `UnitNoLongerAvailable`. That was observed at cede66fe and, per the builder's record, at 84caeffd.
5. **HIGH 2 is closed on the record, not on observation.**
   - The 12ec6b88 code it concerned is deleted.
   - Its replacement touches only the loser path of a race. No §18 criterion's text depends on that path.
   - The criteria keep their TEST-gate evidence, because the first-attempt bodies are unchanged.
   - The race path (backlog rows 49, 50, 54 and 59) rests on the builder's run only.

### Earlier findings: disposition

- **HIGH 1:** closed (row 65).
- **HIGH 2:** closed as above.
- **MEDIUM 3 (ADR-365):** closed.
  - The coordinator's §1 Amended row carries the narrow wording.
  - `U_X4b` holds an allow-list containing `IBillableChargeRecorder` only. It flattens generic constructor parameters and flags any Billing-namespace, Billable or Invoice type.
  - Invoices go only through `IssueInvoiceFromChargesCommand` via the dispatcher.
- **MEDIUM 4:** partly closed.
  - I-F3b: a High incident at 22:30 Dhaka reaches the owner's inbox through real delivery passes (A31).
  - `A41_...`: the final invoice is 3000.00 with the deposit offered for `allocate`, not applied.
  - The rest is in row 64.
- **LOW 5:** gone with the lock.
- **LOW 6:** now row 62.
- **LOW 7:** done. The TC sheet now has 216 rows, 193 PASS, 0 FAIL, and adds T-R1c, U-X4b, A41 and I-F3b.
- **LOW 8:** done for EMR (69) and IMP (63). The XCT rows 2, 3 and 21 are still for the integrator at merge.

### New findings (none blocking)

- **[LOW] R1. `ClaimRetry.cs:24-29`.** After attempt 1 loses, the request-scope DbContext still tracks the failed stay or visit changes, the enqueued notification row and any enlisted charges. Nothing saves that context afterwards: the controller only reads back. But a later `SaveChanges` in the same scope would replay them.
  - Fix: run attempt 1 in a fresh scope too, or clear the change tracker after a lost attempt.
- **[LOW] R2. The 4-attempt bound.** Under heavy contention with many free units, a caller can lose four times and be told `UnitNoLongerAvailable` while units remain. This is bounded and rare. Record it only.
- **[LOW] R3. `docs/test-cases/TC-HO-BRD-001.md`, run-record section.** It contains the full `CATECOMMERCE_TEST_DB` connection string, password included. It is a local password, but it is committed and would merge into master.
  - Fix: redact it before merge.
- **[LOW] R4. `docs/backlog/BRD.md` row 61** still reads "Decide with Billing whether Boarding requests the allocation". The owner decided "Apply automatically" (BIL-001-FU2). Record that on row 61, as the coordinator asked.
  - "Measure at merge" (p95) and "Ship, follow-up fixes" (owner phone, deposit outcome and amount, `canOverrideGate`) are recorded nowhere on the branch either. Add one row each, or a §1.1 line.
  - This gate is read-only, so it does not make these edits.

### Criteria at 84caeffd

All 50 remain MET, as in the first pass. Two are strengthened:
- A31: I-F3b, builder-run.
- A41: `A41_`, builder-run. Its deposit half stays DEPENDENCY-BLOCKED: the owner has decided "Apply automatically", in BIL-001-FU2.

The upstream halves of A33, A34, A35 and A41 are unchanged and now assigned (BIL-001-FU2, EMR 69, IMP 63). The A44 p95 (I-F11) is deferred by the owner ("Measure at merge"); it was never in the criterion text.

### Scope check (re-check range)

- files in allowed scope: 23/23.
  - Boarding source and tests.
  - `ClaimRetry`, `IFreshScopeExecutor` and `FreshScopeExecutor`, under §13's helper and port translations.
  - The handover's §1.1 and the coordinator's §1 row.
  - The ADR-350..369 block.
  - The BRD, EMR and IMP backlog rows: the EMR and IMP rows are the mirror rows the coordinator directed.
  - The TC sheet.
- files outside allowed scope: none.
- §4 out-of-scope work: none.
- Undeclared deviations: none.
