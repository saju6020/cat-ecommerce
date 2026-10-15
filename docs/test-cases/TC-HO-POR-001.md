# TC-HO-POR-001 -- Customer Portal (backend), phases A-D built (re-order retired; stays and subscriptions deferred by owner)

Handover: docs/handover/HO-POR-001.md (sections 15, 16 and 18)
Branch: ho/POR-001 (from master 4a4acb88). Written 2026-10-09 (developer); executed and updated by the TEST gate 2026-10-10/11 on head 38a1c2f0 plus the gate's test-only commit (hash in docs/gates/HO-POR-001-TEST.md).
Scope of this gate: "after Phase D" (HO-POR-001 1 Amended 2026-10-10): stays and subscriptions deferred by owner, portal re-order retired.

> Statuses record what was **observed**. `PASS` = watched passing.
> `PASS (scope)` = the case passes for every section built in the gate's scope (after Phase D); the parts that belong to deferred phases (stays, subscriptions, signed URLs and observation notes) are named in the evidence and under "Cases I could not run". It does not claim those parts pass.
> `BLOCKED-ON-<area>` = the case needs an upstream area that is not merged on master (HO-POR-001 17 and 20.1) and is **not stubbed in src/**;
> it is written here so the phase that unblocks it has its map. `RETIRED` = removed by README C-6 (the stay diary is HO-BRD-001's).
> Rows prefixed `X-` were added by the developer beyond the handover's tables.

## Scope built on this branch

| Phase | Upstream | State |
|---|---|---|
| A | CAT, ACC, ADM, APT (all merged) | **Built**: the section 7 spike, enumeration `PortalSectionState`, constants, `PortalSectionComposer`, `IPortalSectionReader<T>`, cats / appointments / profile readers, Q1, `PortalController` dashboard |
| B | EMR, PHM (both merged) | **Built**: visits reader and prescriptions reader |
| C | BIL (merged) | **Balances reader built** |
| D | SHP, IMP, ORD (all merged) | **Orders reader built** (passes Orders' `canReorder` and `trackingNumber` through). Re-order RETIRED (owner-delegated decision 2026-10-10, owner to review): the customer uses Orders' `POST /api/customers/me/orders/{id}/reorder`; no `ReorderPreviousOrderCommand`, endpoint or `ReorderLineOutcome` here |
| E | BRD (**not merged**) | **DEFERRED by owner** (owner-delegated decision 2026-10-10, owner to review): stays reader not built. Q2 diary and its endpoint are RETIRED (README C-6) |
| F | SUB (**not merged**) | **DEFERRED by owner** (owner-delegated decision 2026-10-10, owner to review): subscriptions reader not built |

Decision recorded in HO-POR-001 1 Amended: the handover gives no placeholder guidance for sections whose readers do not exist yet, so the
dashboard response has **no key** for them (`stays` and `subscriptions`, both deferred by owner) until their phase adds the key, the reader and the registration.

## Summary

| | Count |
|---|---|
| Handover cases (section 15: 40, section 16: 27) | 67 |
| PASS (watched passing; I-B1, I-B2, I-B3, I-B7, I-X1 are "PASS (scope)": sections after Phase D) | 31 |
| FAIL | 0 |
| BLOCKED | 0 |
| NOT RUN | 0 |
| RETIRED by owner decision (re-order 22: U-C1..C12, I-A4, I-C1..C9; diary 12, README C-6: U-D1..D7, I-D1..D5) | 34 |
| DEFERRED-BY-OWNER (stays U-B14, subscriptions U-B15; not executable until HO-BRD-001 / HO-SUB-001 are built) | 2 |
| X- rows (developer X-1..X-18; TEST gate X-19..X-21) | 21, all PASS |
| Regression rows R-1..R-10 (earlier gates) | 10: 8 PASS, 2 n/a (open backlog items, no code to regress) |

31 + 34 + 2 = 67. RETIRED and DEFERRED rows were not executed and are not counted as PASS.

Test counts observed (2026-10-10/11, head 38a1c2f0 plus the TEST gate's test-only changes): Domain 1886, Application 427 (47 Portal), Infrastructure 174, Integration 1476 run (Portal filter: 33 matched, 33 passed; 3 of those live in the Unit folder of the Api-referencing project).

## Environment

- Worktree D:/my-repo/cat_ecommerce-wt/POR-001, branch ho/POR-001. `dotnet build CatEcommerce.sln -p:NuGetAudit=false`: 0 warnings, 0 errors.
- Database: ONLY `catecommerce_por` (created for this branch and migrated to master with `dotnet ef database update --connection`). `CATECOMMERCE_TEST_DB` is the full connection string,
  exported in the same command as every run. `PortalApiFactory` throws at construction when it is unset or names `catecommerce_dev` or `catecommerce_test` (observed: a run without the variable failed all tests at once).
- Suites (TEST gate, 2026-10-10/11): Domain 1886 passed; Application 427 passed (47 Portal); Infrastructure 174 passed; Integration: see "Full-suite result (TEST gate)".
- Hosts: one in-process TestServer per test class (FakeTimeProvider that does not tick, real-clock cookie expiry), plus derived hosts (`WithWebHostBuilder`) for I-B5 and I-X1.
- No in-memory or SQLite provider; no TRUNCATE or DROP; every test mints its own customers and asserts only their own data (16.1).
- Client `DateTimeOffset` with an offset: this slice **writes nothing** (no command, no persisted field), so there is no value to normalise; the tests send UTC instants only.

## Unit cases (handover section 15)

Tests are in `tests/CatEcommerce.Application.Tests/QueryHandlers/Portal/` (`PortalConventionTests`, `PortalReaderTests`, `PortalDashboardComposerTests`) unless named otherwise.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | Reflection over every `.Portal` type | No aggregate, event, configuration or event handler (ADR-257) | PASS | `PortalConventionTests.U_A1_NoPortalTypeIsAnAggregate_AnEvent_AConfiguration_OrAnEventHandler`, `U_A1_ThereIsNoPortalSchemaTableOrDbSet` |
| U-A2 | Constructors in `Read.QueryHandlers.Portal**` | No DbContext, dispatcher or repository (ADR-258) | PASS | `PortalConventionTests.U_A2_NoPortalQueryHandlerOrReaderTakesADbContext_ADispatcher_OrARepository` |
| U-A3 | Every `Portal*` ViewModel, recursive | No property of another module's ViewModel (ADR-259) | PASS | `PortalConventionTests.U_A3_NoPortalViewModelHoldsAnotherModulesViewModel_Recursively` |
| U-A4 | `PortalController` | `[RequireCustomerSession]` on every action; no route contains `pay` (ADR-264) | PASS | `tests/CatEcommerce.IntegrationTests/Unit/PortalControllerConventionTests`: `U_A4_EveryPortalAction_...`, `U_A4_NoPortalRouteContainsPay`, `ThePortalController_InjectsTheDispatcherAndTheUserContextProviderOnly` (lives in the Api-referencing project) |
| U-A5 | Visits and prescriptions section ViewModels | Only the 8.6 allow-listed names (ADR-262) | PASS (2026-10-10: prescriptions added: `PortalReaderTests.U_A5_ThePrescriptionsSectionTypesHaveOnlyTheAllowListedProperties`; was PARTIAL) | Visits: `PortalReaderTests.U_A5_TheVisitsSectionTypesHaveOnlyTheAllowListedProperties` and `PortalConventionTests.U_A5_NoPortalViewModelPropertyIsNamedLikeClinicalContent` PASS. Prescriptions half: the prescriptions test named in the Status column |
| U-B1 | Balances reader throws | balances `Unavailable`, data null, others Ready, one Error log with section and correlation id | PASS | `PortalDashboardComposerTests.U_B1_ABalancesReaderThatThrows_...`, `U_B1_TheLogLineCarriesNoCustomerDataOrUpstreamPayload` |
| U-B2 | Reader throws `OperationCanceledException` | Propagates | PASS | `PortalDashboardComposerTests.U_B2_ACancelledRead_Propagates_NoSectionResult` |
| U-B3 | 13 cats for limit 12 | 12 items, `hasMore`, `OwnerId = caller`, `IncludeDeceased = false`, `PageSize = 13` | PASS | `PortalReaderTests.U_B3_...` |
| U-B4 | Zero cats | Ready, `hasCats = false` | PASS | `PortalReaderTests.U_B4_ZeroCats_HasCatsFalse_NoVaccinationCalls` |
| U-B5 | Cats section Unavailable | `cats.data = null`, `hasCats` absent | PASS | `PortalDashboardComposerTests.U_B5_...` (serialised JSON has no `hasCats`) |
| U-B6 | Q4 throws for one of three cats | That cat Unavailable and null; others mapped; section Ready | PASS | `PortalReaderTests.U_B6_OneVaccinationReadThrows_...`, `U_B6_AVaccinationReadThatCancels_IsNotSwallowed` |
| U-B7 | Q4 only with ids from Q2 | Recorded ids equal Q2's (first 12) | PASS | `PortalReaderTests.U_B7_...` |
| U-B8 | Appointments reader | `upcoming`, page size 5, fields mapped, branch zone carried | PASS | `PortalReaderTests.U_B8_...` |
| U-B9 | Visits reader: draft and two finalised visits for one cat | Only the latest finalised; draft never appears | PASS (adapted) | `PortalReaderTests.U_B9_...`, `Visits_AskForOwnerFilteredAliveCats_...`. EMR's list is finalised-only and has no status field, so the "draft" half is proved against the real module by I-B2 (draft visit of a second cat absent). See 1 Amended |
| U-B10 | Prescriptions reader | Counts 2 and 1, earliest end date, no medicine or dose | PASS (adapted) | `PortalReaderTests.U_B10_Prescriptions_ThreeActiveAcrossTwoCats_CountsAndEarliestEnd_NoMedicineOrDose` (end date is Pharmacy's `CourseEndsOn`; inactive rows dropped) |
| U-B11 | Balances: BDT 1500 and USD 20 | Two entries, no total; zero -> `hasOutstanding = false`, `[]` | PASS (adapted) | `PortalReaderTests.U_B11_...`. Billing returns ONE commerce-currency Money, so each Money is one entry and is never added to another (1 Amended). Also `Balances_OpenInvoicesOnly_OldestDueFirst_LimitFive_...`, `Balances_WalksInvoicePages_...` |
| U-B12 | Every reader passes caller id, UserContext, CorrelationId | Yes | PASS | `PortalReaderTests.U_B12_EveryReader_...` (cats, appointments, profile, visits, balances), `PortalDashboardComposerTests.U_B12_TheCallerIsTheSessionUser...` |
| U-B13 | Orders reader: `canReorder` per status | false, true, true, true | PASS (adapted) | `PortalReaderTests.U_B13_Orders_CanReorder_PerStatus_FollowsOrdersOwnRule` (8 statuses): true only for Cancelled, Delivered, Returned, Refunded = Orders' own order-detail rule, which wins over OQ-2's default (ADR-268); `U_B13_Orders_LimitFive_...` |
| U-B14 | Stays reader | Current and upcoming only; `hasDiary` for CheckedIn / CheckedOut | DEFERRED-BY-OWNER | Phase E deferred (owner-delegated decision 2026-10-10, owner to review): HO-BRD skipped |
| U-B15 | Subscriptions reader | Counts; `nextDelivery`; none -> null | DEFERRED-BY-OWNER | Phase F deferred (owner-delegated decision 2026-10-10, owner to review): HO-SUB skipped |
| U-B16 | Composer invokes readers sequentially | No overlap | PASS | `PortalDashboardComposerTests.U_B16_...` (also asserts the fixed order) |
| U-C1 | Another customer's / unknown order | `OrderNotFound`, cart untouched | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C2 | `PendingPayment` order | `OrderNotReorderable` | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C3 | Three eligible lines, empty cart | Three Added; one save; order never saved | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C4 | Cart holds 2 of 3 / 3 of 3 | AddLine 1 / AlreadyInCart | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C5 | Eligibility reasons | Five outcomes, none added | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C6 | Sellable 1 for wanted 3 | AddedWithReducedQuantity | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C7 | Nothing eligible | Success, zero added, no write | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C8 | `AddLine` throws on line 2 of 3 | Line 2 NotAdded; one save | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C9 | Charge line on a combined order | NotAProduct | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C10 | Price differs | `priceChanged` | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C11 | No cart exists | Opened through Orders' factory | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-C12 | Validator, empty `OrderId` | Fails on `OrderId` | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| U-D1 .. U-D7 | Diary composition | -- | RETIRED | README C-6 / handover 8.8: the diary is HO-BRD-001's; no `GetStayDiaryQuery` here |

## Integration cases (handover section 16)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-A1 | Host starts with `ValidateScopes` + `ValidateOnBuild` | Every reader and upstream handler resolves | PASS | `PortalDashboardEndpointTests.I_A1_TheComposingHandlerAndItsReadersResolve`. This is also the **7 spike**: a Scoped query handler that takes other `IQueryHandlerAsync<,>` instances by constructor resolves under scope validation and runs through `IInMemoryDispatcher` (first dashboard call 200) |
| I-A2 | EF model before and after | No new entity, table or migration; probe migration is empty | PASS | `PortalDashboardSectionsEndpointTests.I_A2_ThePortalOwnsNoEntityTableOrMigration`; CLI: `dotnet ef migrations add PortalProbe` (connection via `ConnectionStrings__CatEcommerce` = `catecommerce_por`) produced `Up` and `Down` with empty bodies and an unchanged model snapshot; the two probe files were deleted and the snapshot restored (`git status` clean of Migrations) |
| I-A3 | Each endpoint without a session; staff session only | `401 session_invalid` both | PASS | `PortalDashboardEndpointTests.I_A3_...` (dashboard only: the portal has one endpoint; re-order is retired) |
| I-A4 | Re-order without `X-CSRF-Token` | `403 csrf_token_invalid` | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review): the portal has no re-order; Orders' own endpoint is used |
| I-B1 | New customer, nothing recorded | cats Ready `hasCats=false`; every other section Ready and empty | PASS (scope) (2026-10-10: also `PortalOrdersEndpointTests.Orders_ACustomerWithNoOrders_...`, `PortalPrescriptionsEndpointTests.Prescriptions_ACustomerWithNone_...` PASS; stays and subscriptions deferred by owner, key absent by design) | `PortalDashboardEndpointTests.I_B1_...` and `PortalDashboardSectionsEndpointTests.I_B1_...` PASS for customer, cats, appointments, visits, balances. prescriptions and orders: the empty-state tests in `PortalPrescriptionsEndpointTests` / `PortalOrdersEndpointTests` PASS; stays and subscriptions deferred by owner |
| I-B2 | Full customer | Every section Ready and populated; deceased cat and draft visit absent | PASS (scope) | `PortalDashboardSectionsEndpointTests.I_B2_...` PASS (TEST gate re-run after the slot fix): two alive cats + one deceased (with a finalised visit), booked appointment, issued invoice 1500.00, finalised visit (Mitu) and draft visit (Rex): deceased cat absent, draft and deceased-cat visits absent, balances populated. Orders and prescriptions populated: `PortalOrdersEndpointTests.I_B2_Orders_...`, `PortalPrescriptionsEndpointTests.I_B2_Prescriptions_...` PASS (their own hosts). Stays, subscriptions: deferred by owner |
| I-B3 | Two customers, each with cat, order, invoice | Each sees only its own | PASS (scope) | `PortalDashboardSectionsEndpointTests.I_B3_...` PASS (cats, visits, invoices, name, balance amount; a forged `customerId` / `ownerId` query string is ignored). Orders part: `PortalOrdersEndpointTests.I_B3_Orders_...` PASS (X-17) |
| I-B4 | Vaccination status vs `GET /api/cats/{id}/vaccination-status` | Identical | PASS | `PortalDashboardEndpointTests.I_B4_...` |
| I-B5 | Billing handler replaced by a throwing fake | 200; balances Unavailable, data null; others Ready; Error log with correlation id | PASS | `PortalDashboardSectionsEndpointTests.I_B5_...` (test-host override via `WithWebHostBuilder`, no production switch; log line `...PortalSectionComposer: Portal section balances unavailable. CorrelationId=<guid> | ...billing is down`; the level is proved by U-B1) |
| I-B6 | 13 alive cats | 12 returned, `hasMore: true` | PASS | `PortalDashboardEndpointTests.I_B6_...` |
| I-B7 | Raw JSON | No key diagnosis, soap, dose, frequency, strength, instructions, allergies, handlingNote, notes anywhere | PASS (scope) | `PortalDashboardSectionsEndpointTests.I_B7_...` also checks `followUpOn`, `problems`, `treatments` and that neither the discharge instructions nor the diagnosis name occur in the body. Re-run when Phase E/F land |
| I-B8 | 20 sequential calls | p95 <= 1500 ms | PASS | `PortalDashboardSectionsEndpointTests.I_B8_...` (acceptance fix round): **p95 = 126 ms** (min 92, median 104, max 171; 20 calls after a warm-up, development machine) for a customer with records in every built section: profile, 2 alive cats (one with a vaccination record), a booked appointment, a finalised visit, an active prescription issued through the Pharmacy vet endpoint, an issued clinic invoice plus the placed order's own invoice, and a placed order made through the Ordering customer endpoints. The test asserts all seven sections are Ready and non-empty before timing. Not measured: the 12-cat worst case (POR 1), and the stays and subscriptions sections (deferred by owner, not built). Replaces the earlier 27 ms figure, which was for a customer with no orders or prescriptions |
| I-C1 .. I-C9 | Re-order cases | -- | RETIRED | Re-order retired (owner-delegated decision 2026-10-10, owner to review). Orders' own `POST /api/customers/me/orders/{id}/reorder` is covered by HO-ORD-001's I-E6 |
| I-D1 .. I-D5 | Diary cases | -- | RETIRED | README C-6; evidenced by HO-BRD-001 / HO-BRD-002 |
| I-X1 | Log capture | No signed URL, note text, customer name, money amount | PASS (scope) | `PortalDashboardSectionsEndpointTests.I_X1_...` PASS (acceptance fix round, POR 20): the customer has a finalised visit with discharge text and an issued invoice of 7351.42; the test asserts the amount is on the board, then that no captured line (registration, arrangement, three dashboard reads) contains the customer name, mobile, discharge text, `7351` or `7,351`. The I-B5 line carries no customer name. Scope: no signed URL or observation note exists in the built sections; full I-X1 (signed URL, observation notes) when Phase E lands |

## Rows added by the developer (X-)

| ID | Case | Status | Evidence |
|---|---|---|---|
| X-1 | Cat item has no handling-note property; cat shows name and flags only | PASS | `PortalReaderTests.CatItems_CarryNameAndFlagsOnly_NoHandlingNote` |
| X-2 | Profile reader maps `fullName` only; a missing profile is an upstream fault (the section becomes Unavailable, not an empty greeting) | PASS | `PortalReaderTests.Profile_MapsFullNameOnly_...` |
| X-3 | Visits reader asks for owner-filtered alive cats, asks EMR per cat with the caller as customer, skips a cat EMR denies with null | PASS | `PortalReaderTests.Visits_AskForOwnerFilteredAliveCats_...` |
| X-4 | Balances: open invoices only, oldest due first, limit 5, open count of the scan, `howToPay` is guidance only, no url/link property | PASS | `PortalReaderTests.Balances_OpenInvoicesOnly_...` |
| X-5 | Balances walks invoice pages until the last or the cap | PASS | `PortalReaderTests.Balances_WalksInvoicePages_UntilTheLastOne_AndStopsAtTheCap` |
| X-6 | ADR-012 guard for the Portal read side (like `BillingConventionTests.U_X6`): no database type, no `AsNoTracking` + whole-entity fetch, no `ToDictionaryAsync`, no `Task.WhenAll`, no dispatcher | PASS | `PortalConventionTests.U_X6_ThePortalReadSide_TouchesNoDatabase_...` |
| X-7 | The only `catch (Exception)` sites are the composer and the per-cat vaccination read (ADR-260) | PASS | `PortalConventionTests.ADR_260_OnlyTheComposerAndThePerCatVaccinationReadCatchExceptions` |
| X-8 | Portal messages carry no link or url constant; offline guidance text | PASS | `PortalConventionTests.TheDashboardMessagesAreGuidanceOnly_NoPayLinkAnywhereInThePortalConstants` |
| X-9 | `GeneratedOn` is the clock instant; the dashboard query has no declared properties (no customer id to set) | PASS | `PortalDashboardComposerTests.GeneratedOn_IsTheClockInstant_AndTheDashboardQueryHasNoCustomerIdProperty` |
| X-10 | `Cache-Control: no-store` on the dashboard | PASS | `PortalDashboardEndpointTests.I_B1_...` |
| X-11 | IDOR: a forged `customerId` / `ownerId` on the query string never changes whose dashboard is returned | PASS | `PortalDashboardSectionsEndpointTests.I_B3_...` |
| X-12 | A deceased cat's finalised visit is not on the dashboard | PASS | `PortalDashboardSectionsEndpointTests.I_B2_...` |
| X-13 | Prescriptions: only active rows, grouped per cat, count + soonest `CourseEndsOn`, ordered soonest first | PASS | `PortalReaderTests.U_B10_...`, `Prescriptions_NoneActive_IsAnEmptyList` |
| X-14 | Orders reader asks for the caller's orders page 1 of `DashboardItemLimit`, newest first; item has no tracking, bank or payment-reference field | PASS | `PortalReaderTests.U_B13_Orders_LimitFive_...`, `Orders_NewestFirst_AndTheItemHasNoTracking...` |
| X-15 | Real HTTP: a placed COD order is on the dashboard with number, status, total, item count; Confirmed -> `canReorder` false | PASS | `PortalOrdersEndpointTests.I_B2_Orders_...` (order placed through Ordering's customer endpoints) |
| X-16 | Real HTTP: after cancelling, `canReorder` is true and equals the order detail page's `canReorder` | PASS | `PortalOrdersEndpointTests.Orders_CanReorderMatchesTheOrderDetailPage_...` |
| X-17 | IDOR: two customers, each sees only its own orders / prescriptions; the other sees `Ready` with an empty list | PASS | `PortalOrdersEndpointTests.I_B3_Orders_...`, `PortalPrescriptionsEndpointTests.I_B3_Prescriptions_...` |
| X-18 | I-B7 on the prescription: raw dashboard JSON holds no medicine name, dose, frequency, strength, route, instructions; row has exactly 4 keys. No bank details / payment reference / recipient data from orders | PASS | `PortalPrescriptionsEndpointTests.I_B2_Prescriptions_...`, `PortalOrdersEndpointTests.Orders_NoBankDetails...` (vet issued the prescription through Pharmacy's endpoint) |

## Rows added by the TEST gate

| ID | Case | Status | Evidence |
|---|---|---|---|
| X-19 | IDOR, transferred cat: after a staff transfer the former owner's dashboard has no cat, `hasCats = false`, no visit, and neither the cat id nor the visit id appears in the cats, visits or prescriptions sections; the new owner sees the cat. A precondition asserts the former owner did see both before the transfer, so the test cannot pass on an empty world | PASS | `PortalOwnershipChangeEndpointTests.ATransferredCat_AndItsVisits_LeaveTheFormerOwnersDashboard_AndTheNewOwnerSeesTheCat` (transfer through `POST /api/cats/{id}/ownership-transfer` as administrator) |
| X-20 | IDOR, merged account: after a staff merge (two accounts sharing a contact number) a third customer's raw dashboard holds no id or name of either merged account; the merged-away account's own session gets 401 on the dashboard; the survivor sees its own name only and (Billing ADR-192) the duplicate's issued invoice | PASS | `PortalOwnershipChangeEndpointTests.AMergedAwayCustomersRecords_NeverAppearOnAThirdCustomersDashboard` |
| X-21 | Time-of-day independence of the booking arrangement: `BookableSlot()` is 10:00 Asia/Dhaka on day +3, never "now + 3 days rounded down" | PASS | Failure reproduced first at 23:41 Dhaka (409 `SlotNoLongerAvailable` from `BookConfirmedAppointmentAsync`, cause confirmed: 23:30 Dhaka start + 30-minute service ends after the 23:45 close); after the fix the Portal filter passed 31/31 at about 23:47 Dhaka, inside the window that used to fail |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R-1 | SECURITY L1 (POR 8) | A deceased cat's active prescription was listed | Deceased cat's active prescription absent | PASS | `PortalPrescriptionsEndpointTests.I_B2_Prescriptions_ADeceasedCatsActivePrescription_IsNotShown` (HTTP) and `PortalReaderTests.Prescriptions_ADeceasedCatsActivePrescription_IsNotShown` (unit) |
| R-2 | SECURITY L2 (POR 9) | No rate limit on the fan-out read | None: the handover requires none; open backlog item, no code to regress | n/a | see "Gaps left deliberately" |
| R-3 | CODE_REVIEW M (POR 1) | N+1 visits read | None: open backlog item, not fixed in this scope | n/a | see "Gaps left deliberately" |
| R-4 | CODE_REVIEW L (POR 3) | One cat's visit failure makes the whole visits section Unavailable | By design for visits (only vaccination degrades per cat); U-B6 and the ADR-260 guard cover the rule | PASS | `PortalReaderTests.U_B6_...`; `PortalConventionTests.ADR_260_OnlyTheComposerAndThePerCatVaccinationReadCatchExceptions` |
| R-5 | CODE_REVIEW L (POR 6) | U-B13 asserted a private copy of the rule | Now asserts Orders' own `CustomerOrderRules` | PASS | `PortalReaderTests.U_B13_OrdersCanReorderRule_PerStatus_...` and X-16 (HTTP: list `canReorder` equals the detail page's) |
| R-6 | Finalise | `followUpOn` on visits | Passed through unchanged | PASS | `PortalReaderTests.Visits_FollowUpOn_IsPassedThroughFromTheEmrListItem`; I-B7 allow-list |
| R-7 | Finalise | `trackingNumber` and `canReorder` on orders | Passed through, null before dispatch | PASS | `Orders_CanReorderAndTrackingNumber_ArePassedThroughFromOrdersListItem`; `PortalOrdersEndpointTests.I_B2_Orders_...` |
| R-8 | SECURITY (checked, no finding) | Transferred and merged accounts lose access | Behaviour proved over HTTP | PASS | X-19, X-20 |
| R-9 | CODE_REVIEW L (POR 5) / ARCHITECTURE | Stale TC sheet wording | Sheet rewritten by this gate | PASS | this sheet |
| R-10 | Re-order retired | No portal re-order endpoint exists; no payment route | Controller conventions | PASS | `PortalControllerConventionTests` (every action `[RequireCustomerSession]`, no `pay` route) |

## Acceptance-criteria coverage (HO-POR-001 18, scope after Phase D)

| Criterion | Covered by | Status |
|---|---|---|
| A1 cats, appointments, orders, balances together (stays deferred) | U-B3, U-B8, U-B11, U-B13, I-B2 | PASS (stays part deferred by owner) |
| A2 `hasCats: false`, never when cats failed | U-B4, U-B5, I-B1 | PASS |
| A3 visits, prescriptions, vaccination status on the dashboard (subscriptions deferred) | U-B6, U-B9, U-B10, I-B2, I-B4 | PASS (subscriptions deferred by owner) |
| A4 only own data; deceased cats and draft visits never | U-B7, U-B9, I-B2, I-B3, X-11, X-12, X-17, X-19, X-20, R-1 | PASS |
| A5 one failing module leaves the rest usable | U-B1, U-B2, U-B5, I-B5 | PASS |
| A6..A9 re-order | -- | RETIRED (owner 2026-10-10) |
| A10..A12 stay diary | -- | RETIRED (README C-6) |
| A13 no clinical content | U-A5, U-B10, I-B7, X-18 | PASS |
| A14 no payment action | U-A4 | PASS |
| A15 no persistence | U-A1, U-A2, I-A2 | PASS |
| A16 customer session on every endpoint; scope validation on | U-A4, I-A1, I-A3 | PASS |
| A17 p95 <= 1.5 s | I-B8 (p95 126 ms, every built section populated incl. an active prescription and a placed order) | PASS; the 12-cat worst case is not measured |

## Full-suite result (TEST gate)

Run on `catecommerce_por` only, `CATECOMMERCE_TEST_DB` exported in the same command. Build 0 errors; 3 warnings, none in Portal files (two CS0108 in Api/Read, one xUnit2000 in the Ordering tests, all from master).

- Domain 1886 passed; Application 427 passed (47 Portal); Infrastructure 174 passed.
- Integration, whole project, first full run: **1476 total, 1472 passed, 2 skipped, 2 failed**, neither in the Portal:
  - `ShopCataloguePerformanceTests.I_E11_...`: known flake, also fails alone (p95 580 ms against a 500 ms budget; fix in progress on another branch). Shop code, reported not hidden.
  - `OrderingPaymentEndpointTests.AReferenceThatLooksLikeACardNumber_...`: failed once in the full run (`TransferAmountMismatch` where `ReferenceLooksLikeCardNumber` was expected; the test hard-codes a grand total of `2135.00m`); passed alone on 4 of 4 reruns. Not on the known-flake list; POR backlog 15 for the ORD owner.
  - The other known flakes (I_E4, FU1_T1, XI_9, I_C10, I_G5_AChangedFollowUp, I_D13, I_X1, I_F4) did not fail in this run.
- Portal filter after the gate's two new tests: 33 matched, 33 passed.

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| U-B14, U-B15 | Stays and subscriptions readers do not exist (owner deferral 2026-10-10) | HO-BRD-001 and HO-SUB-001 merged, then portal phases E/F |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| A transferred cat still appears by name and id in the former owner's own appointment list (observed while writing X-19) | Appointments' read; the Portal passes its row through | APT owner (POR 14) |
| Rate limit (POR 9) and visits N+1 (POR 1) | Open backlog items, not required by the handover | Integrator |
| I-B8 at the 12-cat worst case | Measured with 2 cats (all built sections populated); about 30 round trips at 12 | Integrator, with POR 1 |
