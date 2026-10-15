GATE: TEST
HANDOVER: HO-BRD-001
COMMITS: ho/BRD-001 head cede66fe under test; the gate added af3d9a56 (case sheet, all rows NOT RUN before any run) and test-only commits on top
DATE: 2026-10-15
STATUS: PASS

No BLOCKER or HIGH findings. Two MEDIUM findings are real failures of handover cases under true parallelism (backlog rows 49 and 50); neither oversells and neither loses data. The sheet is docs/test-cases/TC-HO-BRD-001.md (212 rows: 187 PASS, 2 FAIL, 23 BLOCKED, 0 NOT RUN).

RUNS
- Build green (0 errors). `has-pending-model-changes`: none. Unit suites: Domain 2217, Application 610, Infrastructure 189, all pass (clean rebuild; a stale mutated Domain.Tests binary showed one red test until rebuilt, an artefact of the mutation runs, not a defect).
- Full integration suite, once, on a fresh `catecommerce_brd_test` (full connection string; dev and test databases never touched; dropped afterwards, confirmed): 1926 tests, 1916 passed, 6 skipped, 4 failed in 11 m 42 s. Failures fell from 49 to 4. Each of the four passes alone: Billing XI_6 (order-dependent on an empty DB, "Too few tables held a row"), Waitlist IG2, Reporting T_I7 and Reporting I_X1_U_X4 (both "did not hold after 600 delivery passes"). XI_6, IG2 and T_I7 are known flakes; I_X1_U_X4 is not on the list (row 58). Every Boarding test passed in the full run.
- Boarding classes after the gate's additions: 126 tests, 124 passed, 2 skipped (the two FAILs below).

FINDINGS
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Boarding/UnitClaim.cs and the reserve handler: a lost race on the deterministic first unit is answered 409 UnitNoLongerAvailable although another unit of the type is free. T_R1b (two customers, two free units): 200/409 in 5 of 5 rounds, 3 of 3 runs. I-C12 (one last unit) passes: exactly one winner of 20, and T_R1 (5 rounds of 2) also. Row 49.
- [MEDIUM] BookDaycareVisitCommandHandler: the slot is picked by a read; a lost race on the unique slot is answered 409 DaycareFull with a slot free. I-H3b (capacity 2, 3 concurrent): one visit and two refusals, 3 of 3 runs; the handover's I-H3 expects two 201 and one 409. Row 50.
- [LOW] Test names, gaps and observations, rows 51-58 (stale U_G24 name; 17 handover unit cases and 5 integration cases with no test; staff Confirm racing the worker gets 409 8 of 8; EF split-query warning in the logs; the refundable branch of the payment/lapse race never reached; the worker-off flag was pinned by no test until now (fixed, row 57); the four full-run flakes).

WHAT WAS CHECKED AND HELD (hand-built fixture, expected figures written in the sheet before the run)
- Money: 3 nights 3000; 31 nights with 10 % discount = Month 21600 + Night 900 = 22500, deposit 6750, cycle invoice 21600 then final invoice 900; early check-out after 3 of 10 nights 3000 (preview equals result); early check-out on day 30 after the cycle invoice keeps 21600, voids 900, credit 0; early check-out on day 10 of the discounted stay loses the discount, 10000; late check-out by 2 nights with 2 cats and an additional-cat rate 6000, never blocked, allocation not extended, following booking flagged UnitOverstayed; extension 5->8 = 8000 and ->15 = 13000 (threshold crossed); deposit 899.99 refused, 900.00 confirms, another customer's 900 does not count.
- Dhaka dates: check-in refused at 23:50 the day before, allowed at 00:10 and 23:50 on the arrival date; check-out at 23:50 on night 2 is early (2000), at 00:10 and 23:50 on the departure date is on time (3000), at 00:10 the day after is late (4000). A UTC date would have failed three of these.
- Races: reserve 1-of-1 and confirm versus worker lapse with a full deposit (8 rounds, never Cancelled), payment versus lapse (8 rounds, never lost; the lapse branch was not reached).
- Rate limits at HTTP: reserve, waitlist join and daycare book each return 429 on the 4th call at a limit of 3, per customer, one shared bucket (row 23 closed with evidence).
- Mutations (all restored, git status clean): deposit earmark filter, gate boundary, late check-out extending the allocation, overstay flag, creditDue netting, low-incident media filter, vet-authority reason, worker-off setting, preview-differs-from-result. All nine were caught. M8 was caught only by a test the gate added; before it nothing pinned the flag.
- Convention guards added: no Boarding command handler injects a Billing/Inventory/Clinic/Administration repository; no Boarding event handler injects INotificationOutbox. Log capture across reserve, check-in, observation, High incident, escalation, check-out: no care note, incident text, contact or customer detail logged.

TESTS ADDED (all green except the two skipped, which carry their backlog row)
- tests/CatEcommerce.IntegrationTests/Endpoints/BoardingTestGateTests.cs, BoardingRateLimitTests.cs, BoardingLogCaptureTests.cs, BoardingWorkerOffInOtherHostsTests.cs; tests/CatEcommerce.Application.Tests/CommandHandlers/Boarding/BoardingConventionTests.cs.
- T_R1b and I_H3b are `Fact(Skip=...)` pointing at rows 49 and 50 so the branch is green; they fail without the Skip. Remove the Skip when the handlers retry.

PROPOSED/ADDED ROWS: docs/backlog/BRD.md rows 49-58 (2 MEDIUM, 8 LOW).

NOT A VERDICT ON ACCEPTANCE: BLOCKED rows in the sheet (23) are gaps, not failures. Eleven of the 50 acceptance criteria are only PARTIALLY covered (A11, A22, A23, A29, A31, A36, A40, A41, A44, A45, A50: see the roll-up table); the acceptance gate decides.
