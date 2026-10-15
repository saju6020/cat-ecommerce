GATE: ACCEPTANCE
HANDOVER: HO-POR-001
COMMITS: 68e0be0f 2cf203e0 9b1fd21b 05ebee45 f1cae602 55339e38 a0127b48 a45a85e8 66e34963 9fb2917a e57d1731 38a1c2f0 7d6ea794 (ho/POR-001, merge-base master a8761829; head 7d6ea794)
DATE: 2026-10-11
STATUS: FAIL

One criterion lacks evidence: A17. The other in-scope criteria are met, and the retired or deferred ones are covered by
recorded decisions. The fix is a test only (see the HIGH finding below). After it, a short ACCEPTANCE re-check of A17 is
enough: under the tiny-fix rule no other gate needs to run again.

PRIOR GATES (from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS (e57d1731)   SECURITY: PASS (9fb2917a)   CODE_REVIEW: PASS (66e34963)   TEST: PASS (7d6ea794)
- Fix round 38a1c2f0 (deceased cat's prescriptions, stale wording, backlog renumber) came after ARCH/SEC/CODE and before TEST.
  The TEST gate ran on 38a1c2f0 and covers it: TC row R-1 has a unit test and an HTTP test. Under the tiny-fix rule no other
  re-review is needed.
- TC sheet docs/test-cases/TC-HO-POR-001.md: 31 PASS, 34 RETIRED, 2 DEFERRED-BY-OWNER, 0 NOT RUN, 0 BLOCKED. RETIRED and
  DEFERRED rows are not counted as passes here.
- Re-run by this gate: `dotnet test tests/CatEcommerce.Application.Tests --filter Portal`. Result: 47 passed, 0 failed.
  Integration tests were not re-run; I rely on the TEST gate's record (Portal filter 33/33).

CRITERIA (§18; scope "after Phase D" per §1 Amended 2026-10-10 finalise)
- A1 MET (stays part DEFERRED by owner, §1 Amended (1)). Evidence:
  - The composing handler GetPortalDashboardQueryHandler returns every built section in one ViewModel.
  - U_B16 and U_B12 run all 7 readers into one response.
  - Over HTTP: I_B2 (PortalDashboardSectionsEndpointTests) covers cats, appointments and balances; PortalOrdersEndpointTests.I_B2_Orders_... covers orders.
- A2 MET. U_B4 (hasCats false); U_B5 (an Unavailable cats section serialises with no hasCats key); I_B1 in PortalDashboardEndpointTests.
- A3 MET (subscriptions DEFERRED by owner). U_B6, U_B9, U_B10; I_B2 (visits); PortalPrescriptionsEndpointTests.I_B2_Prescriptions_...;
  I_B4 (vaccination status is identical to the cat endpoint).
- A4 MET. Tests:
  - U_B7; I_B2 (deceased cat, draft visit and the deceased cat's visit are absent)
  - I_B3 (two customers; a forged customerId or ownerId is ignored)
  - the orders and prescriptions I_B3 tests
  - R-1 (a deceased cat's prescription is absent)
  - X-19 and X-20 (transferred cat, merged account)

  Source check: CustomerId comes only from UserContext, and every reader passes only the caller's id.
- A5 MET. U_B1, U_B2, U_B5, ThePrescriptionsAndOrdersReaders_FailOnTheirOwn_...; I_B5 replaces the Billing handler with a
  throwing fake in the test host. Source check: PortalSectionComposer returns Unavailable with null data and rethrows OperationCanceledException.
- A6..A9 RETIRED by recorded decision. §1 Amended 2026-10-10 (3) (owner-delegated, owner to review): the customer re-orders
  through Orders' POST /api/customers/me/orders/{id}/reorder. Source check: no re-order code is on the branch.
- A10..A12 RETIRED (README C-6, §8.8): the stay diary is evidenced in HO-BRD-001/002.
- A13 MET. U_A5 (visits and prescriptions types use only the allow-listed fields); I_B7 (raw JSON, discharge text and diagnosis
  are absent); the prescriptions I_B2 test (the row has exactly 4 keys and no medicine name). Source check: the ViewModels have
  no property that could hold clinical content.
- A14 MET. U_A4_NoPortalRouteContainsPay; the controller has a single GET action; howToPay is a fixed text constant (X-8).
- A15 MET. U_A1, U_A2, U_X6, I_A2. The probe migration was empty (recorded in the TC sheet). No change under Infrastructure or Domain.
- A16 MET. U_A4_EveryPortalAction_CarriesRequireCustomerSession; I_A1 (host starts with ValidateScopes/ValidateOnBuild);
  I_A3 (401 with no session and with a staff session). I-A4 is retired along with re-order.
- A17 NOT MET. The criterion asks for p95 within 1.5 s "for a customer with records in every section". The only evidence is
  I_B8, and its customer has records in 5 of the 7 built sections (customer, cats, appointments, balances, visits). The orders
  and prescriptions readers run on every call but return empty lists, and no timed call has an order or a prescription.
  - Both sections are in scope after Phase D and are not deferred. A17 itself is not marked deferred anywhere.
  - The TC sheet and the I_B8 code comment both say this case covers "every section in the gate's scope" / "every section
    built so far". That is overstated: the sheet's own I-B8 evidence row lists only the five sections.

FINDINGS
- [HIGH] tests/CatEcommerce.IntegrationTests/Endpoints/PortalDashboardSectionsEndpointTests.cs:248-267. A17 has no evidence:
  I-B8 times a customer who has no order and no prescription. This is an evidence gap, not an observed defect; the measured
  27 ms leaves a lot of headroom. Fix, test only:
  - time 20 sequential dashboard calls for a customer who has records in all seven built sections, including at least one
    placed order and one active prescription, and record the p95 in the TC sheet;
  - correct the "(scope)" wording on I-B8 and A17 in the sheet.
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Portal/Sections/PrescriptionsSectionReader.cs:28-37. Undeclared side effect
  of fix round 38a1c2f0:
  - The new alive-cat filter reads only the first DashboardCatLimit (12) alive cats, ordered by name.
  - A customer with 13 or more alive cats loses the active-prescription rows of every cat after the twelfth, with no
    indication. Before the fix, every owned cat was listed.
  - It is the customer's own data, so nothing leaks, but the summary is silently incomplete.
  - Not covered by any test (I_B6 has 13 cats and no prescriptions). Backlog POR 17.
- [LOW] Owner review is outstanding for the delegated decisions of 2026-10-10. This PASS-in-waiting rests on them:
  phases E/F deferred, portal re-order retired (A6..A9), EMR FollowUpOn and Orders TrackingNumber/CanReorder edits
  (CONTRACTS 30/31), OQ-4 no access logging, and the invoice cap of 500 (BIL 46). If the owner reverses any of them,
  the affected criteria and files must be judged again. Backlog POR 18 (complements POR 13).
- [LOW] docs/handover/HO-POR-001.md §13/§16. The allowed-scope list was never amended. Modules/PortalModule.cs (accepted under
  ADR-341 as the §12.5 "Program.cs registrations"), the four EMR/ORD source files and the six docs edits are allowed only by
  §1 Amended (8) and the gates' reading of it. §16 still names the shared catecommerce_dev database, while the project rule is
  a per-branch database (catecommerce_por was used, correctly). Handover text drift. Backlog POR 19 (complements POR 12).
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/PortalDashboardSectionsEndpointTests.cs:271. §16 expects I-X1 to
  show "no money amount" in the logs. The test never asserts it: its customer has no invoice, and the money check is not in
  the test. The TC sheet still marks it PASS (scope). Not a §18 criterion. Backlog POR 20.

SCOPE
- Out-of-scope work found: none.
  - No payment surface, no re-order, no stays or subscriptions code, no clinical field, no write from a query.
  - No persistence, migration, Domain or Infrastructure change, and no csproj or sln change.
  - The adr.md changes are append-only (113 added, 0 removed).
- Files outside allowed scope: none, once the authorised exceptions below are counted.

SCOPE CHECK
- Files in allowed scope: 49/49 (diff a8761829..7d6ea794):
  - 38 by §13's literal list, counting Modules/PortalModule.cs as §12.5's registrations (ADR-341) and including the four
    earlier gate records and the TC sheet
  - 4 EMR/ORD source files (additive; ARCHITECTURE verified this) and 6 docs edits (CONTRACTS, four handover Amended rows,
    BIL backlog 46), authorised by §1 Amended 2026-10-10 (8), owner-delegated
  - docs/backlog/POR.md (1), by convention
- Files outside allowed scope: none.

UNDECLARED DEVIATIONS
- PrescriptionsSectionReader drops the prescriptions of cats beyond the twelfth (LOW above, POR 17).
- The I_B8 comment and the TC "(scope)" status claim every built section, but orders and prescriptions are empty in the
  measured customer (HIGH above).
- Everything else that differs from the handover is declared in §1 Amended (2026-10-09, 2026-10-10 build, 2026-10-10
  finalise). Examples: U-B13 now follows Orders' rule (Confirmed is false); U-B9 adapted; the composer takes a correlation-id
  argument; the open-invoice scan cap.

OPEN OWNER ITEMS (do not block; review owed)
- Review of the owner-delegated decisions of 2026-10-10: (1) Boarding and Subscriptions deferred (no stays or subscriptions
  keys; U-B14/U-B15 deferred); (2) gates after Phase D; (3) portal re-order retired in favour of Orders' endpoint (OQ-2 and
  OQ-3 closed); (4) followUpOn on visits (EMR additive, CONTRACTS 30); (5) trackingNumber and canReorder on the order list
  item (ORD additive, CONTRACTS 31); (6) OQ-4 closed: no access logging; (7) the 500-invoice cap kept (BIL 46).
- Still open in §9.1 with build defaults: OQ-1 (Portal not yet in .ai/bounded-contexts.md; architect task), OQ-5 (only
  relevant to the retired diary), OQ-7 (resolved by the §1 reconciliations), OQ-8, OQ-9, OQ-10, OQ-11.

Medium/low findings go to docs/backlog/POR.md rows 17-20 (owner rule 2026-09-27).

---

## Re-check (2026-10-11) -- fix round c1d6d9dd

COMMITS: c1d6d9dd (on top of ba48b321)
STATUS: PASS

- A17 MET. `PortalDashboardSectionsEndpointTests.I_B8_TwentySequentialDashboardCalls_P95IsWithinOneAndAHalfSeconds` now builds a
  customer with records in all seven built sections, made through the owning modules' endpoints:
  - profile, 2 alive cats (one vaccinated), a booked appointment, a finalised visit, an issued invoice
  - an active prescription, issued through Pharmacy's vet endpoint
  - a COD order, placed through Ordering's customer endpoints on an Ordering host over the same database

  Before timing, the test asserts that all seven sections are `Ready` and non-empty. I re-ran it on `catecommerce_por`:
  PASS, p95 149 ms (min 98, median 104, max 156) over 20 calls. The fix round recorded 126 ms; both are far inside 1500 ms.
  The TC sheet's I-B8 and A17 rows are corrected and no longer claim "(scope)". The 12-cat worst case is still not measured
  (POR 1). A17 does not ask for it.
- POR 20 resolved. I re-ran `I_X1_...`: PASS. The invoice of 7351.42 is asserted on the board, and the test asserts that no
  captured log line contains `7351` or `7,351`, the customer name, the mobile or the discharge text.
- POR 17 resolved, within tiny-fix scope. `PrescriptionsSectionReader` now pages the same owner-filtered `GetCatsQuery`
  (`OwnerId = caller`, `IncludeDeceased = false`, page size `PagingRules.MaxPageSize` = 100) until it has every alive cat.
  - About 25 lines in one reader. It is read-only and uses no new upstream, dependency, registration or type.
  - The owner scoping and the deceased-cat filter are unchanged, so no SECURITY or ARCHITECTURE delta is needed.
  - The loop ends on an empty page or once the collected count reaches `TotalCount`.
  - New unit tests: `Prescriptions_ACustomerWithThirteenCats_StillSeesThePrescriptionOfTheThirteenth` and
    `Prescriptions_MoreAliveCatsThanOnePage_ReadsEveryPage` (130 cats, 2 pages).
  - I re-ran the Application Portal tests: 49/49 passed. The fix round reports the Portal integration filter at 33/33; this
    gate re-ran only I_B8 and I_X1.
- Scope of c1d6d9dd: 5 files, all allowed. These are the Portal reader, the Portal tests (Application.Tests and
  IntegrationTests), the TC sheet and the POR backlog. No production change outside `QueryHandlers/Portal/**`.
- Every §18 criterion is now MET, or RETIRED/DEFERRED by a recorded decision (A1 stays and A3 subscriptions deferred; A6..A9
  and A10..A12 retired). All four prior gates have recorded PASS.
- Still open, not blocking:
  - The owner's review of the delegated decisions of 2026-10-10 (POR 18, 13). If the owner reverses any of them, the
    affected criteria and files must be judged again.
  - POR 19 (handover §13/§16 text) and the earlier LOW rows.

FINAL STATUS: PASS
