GATE: ARCHITECTURE
HANDOVER: HO-ORD-001
COMMITS: 663cc93f 2aab161e 9a0bdfb2 fa8296c4 681a4dbc 5471674e 6f43c041 b8f941bf 7d0d0c1c b8479b6d 6e15362f 7f818f05(merge) e3fafc5f 2a5544c7 cf81c2c1 26d21c9b 7b357ce7 900c4c5a (ho/ORD-001, `git diff 9eb87445...900c4c5a`, 283 files)
DATE: 2026-10-09
STATUS: FAIL

What this gate did:
- Read HO-ORD-001 §1 (including the "Amended (build)" row), §4.4, §6, §8, §9, §11, §12, §13, §14, §19 and §20. Read CONTRACTS.md, HO-BIL-001 §8.13, HO-IMP-001 §8.8/§8.14 and HO-SHP-001 §8.12.
- Read every `.ai/` document that §6 names: architecture, cqrs, bounded-contexts, read-side-pattern, host-composition, and ADR-021/070/099/111/179/197-216. §6 is complete.
- Reviewed the changed source. It did not build, run tests or touch a database. Anything that depends on runtime behaviour is marked TEST.
- Judged every deviation in the Amended (build) row on its merits. None was treated as pre-approved.
- Owner rule (2026-09-27): only BLOCKER and HIGH fail the gate. MEDIUM and LOW rows were appended to `docs/backlog/ORD.md` (rows 22-31).

CLEAN — checked and compliant
- **Layers.**
  - Domain imports only Shared, Platform.Core/Common and its own namespaces. No EF, Application, Read or Api type.
  - Application does not reference Read or Infrastructure. Read does not reference Application.
- **Superseded architecture.** None of these appears in the diff:
  - IBusMessageDispatcher, IDispatcher, IEventPublisher, MassTransit, RabbitMQ, Mongo, Bson or QueuePublishOnly;
  - AddServiceBusProvider, a 202 response, a rejection event, CommandServices, IReadRepository or ViewModelBase;
  - a second host, double or float money, `[Authorize]`, a payment-gateway type, or a `.csproj` edit.
  - Program.cs is untouched; registration lives in OrdersModule (ADR-341).
- **Controllers.** They inject only IInMemoryDispatcher, IUserContextProvider, IActiveBranchAccessor, the cookie writer and the guest-token hasher. They mint ids, stamp `ValidateCommand = true` and dispatch one command or query per action. None holds a DbContext, a repository or an aggregate.
- **Registration.** All 38 command handlers, 39 validators (as IValidator<T>; the two child validators are used through SetValidator) and 27 query handlers are registered, plus 9 event-handler registrations. Every handler, port and the work cycle is Scoped. The Singletons are stateless or depend only on IConfiguration. Nothing from AddInMemoryBusServices is hand-registered.
- **Aggregates.**
  - 9 roots, all mapped through ConfigureOrderingRoot: `xmin` row version, ValueGeneratedNever, ADR-021 corrections.
  - Children use OwnsMany/OwnsOne. There is no HasMany.
  - No public setters. Mutations go through aggregate methods and end in UpdateVersion.
  - Other aggregates are referenced by id.
  - Money is numeric(12,2) plus a char(3) currency. Instants are timestamptz. Enumerations are converted on Value.
- **Migrations.**
  - All seven are schema-local (`ordering` and `ops`) and the snapshot diff only adds lines.
  - Raw SQL: the order-number sequence, plus no-delete/no-truncate triggers on sales_order, order_line, order_status_change and order_payment_ref. No-update triggers on the two append-only histories. Append-only triggers on order_refund. Each has statement-level TRUNCATE triggers and a working Down.
  - C-ALL-1: all 19 tables are classified. C-ALL-2/3: an eraser and a retention source are registered.
- **Read side.** AsNoTracking with projections. Lists are paged or capped. No query writes. Cross-context joins (pick list, packing slip) are permitted by `.ai/read-side-pattern.md` §5.
- **Events.** Past-tense names. Handlers save through repositories, never call SaveChangesAsync and never enqueue (ADR-057). The audit handler writes BusinessEvent rows. Messages go only through INotificationOutbox.
- **Published contracts used correctly.**
  - IStockLedger: Reserve, Release, ConsumeReservation, Deduct, ReceiveReturn and Plan, each with the reference and party stamp.
  - IBillableChargeRecorder, IssueInvoiceFromChargesCommand, RecordPaymentCommand, IssueCreditNoteCommand and IssueRefundCommand.
  - IDietAuthorisationGate (the deny-all fake until HO-PHM-001 lands, with the skips as declared), `inventory.v_sellable_stock`, and INotificationLinkBuilder.OrderLink (the one HO-NTF-001 edit).
- **Shared test infrastructure.**
  - `CatEcommerceApiFactory` setting `Orders:Worker:Enabled=false` implements §12.2's "disabled in Testing" through configuration, with the production default `true` in appsettings. It hides no defect from this gate. The lost hosted-loop coverage is CODE_REVIEW backlog 4.
  - The `withIdempotencyPurge` seam is CODE_REVIEW backlog 8.
  - The removed bank-account test is still covered at unit level by ZoneAndChargeTests (CODE_REVIEW backlog 6).
  - The D9/D10 tolerance changes are for TEST to judge against §20.3.
- **Handover document.** Only the §1 Amended row changed. That is the established convention, not a §14 finding.
- **ADR-197…216.** Appended to adr.md with nothing removed. 215 and 216 are within the range §9 reserves.

DEVIATIONS IN THE AMENDED (BUILD) ROW — verdicts
1. **Billing seam / ADR-215 (one database transaction around Billing's self-saving commands): not compliant as recorded. HIGH 4.**
2. **Delivery fee under a 0 % tax class (OQ-6): compliant.** It reads ADM's tax classes AsNoTracking and fails closed. It needs an owner decision; no gate finding.
3. **"Ports Ordering owns because the owning area published projections":**
   - The ICatalogueLookup half is compliant on the ADR-111 / CatalogueSkuDirectory precedent. The row's premise is inaccurate (MEDIUM 2).
   - The BR-14 half is not what the code does: it uses Inventory's internal IStockPositionReader and IInventoryClock, not `Batches`. HIGH 2.
4. **Diet gate on the deny-all fake with Skip markers: compliant.** Known pending.
5. **201 response, required key, `[ConflictAsQuoteChanged]`, stock-race translation: compliant.** One §12.5 mapping is missing (MEDIUM 4).
6. **E (branch-scoped staff commands, the IOrderFinder/IReturnFinder ports, AbsorbDuplicateCustomerCart, OrderFreeText):** compliant.
   - "The held-payment approval records the Billing payment under the first person then approves it as the second": not compliant. It uses an unpublished Billing command and a synthesised UserContext. HIGH 3.
   - The `OrderWorkCycle` seam: CODE_REVIEW backlog 8.
7. **F (webhook inbox ON CONFLICT, ReallocateLine):** the inbox is compliant. The reallocation picks locations from Inventory-internal candidates (HIGH 2).
8. **G:**
   - OrderRefundMethod, IOrderingStaging (C19 only) and refunds-due are compliant.
   - "Ordering applies BR-3 itself" and C24 restock/write-off through Inventory commands: not compliant. HIGH 1.
   - Store credit stays unavailable: compliant.
9. **H (Counter pricing, LoadCounterAsync, DeductAsync, TenderCalculator, ReceiptTextRenderer, `change_given`): compliant.** The seventh migration is outside the six that §12.1/§13 name (MEDIUM 5).
10. **ADR-216 (shared walk-in customer): an owner decision, recorded as Active.** MEDIUM 1, owner question. It uses HO-ACC-001's own RegisterWalkInCustomerCommand unchanged and keeps the order customer-less. Architecturally it is not a gate failure.
11. **Migrations applied by hand to `catecommerce_ord` and regenerated by the integrator: compliant.** CODE_REVIEW backlog 13.

FINDINGS
- [BLOCKER] .ai/flows/order-fulfilment-flow.md:3,66-83 — §14 forbids `.ai/**` except decisions/adr.md.
  - The branch rewrites the flow doc's Status, "Decisions made", "Key files" and "Remaining work".
  - The precedent is the opposite (HO-BIL-001 ARCHITECTURE deviation 18: "No `.ai/flows` updates: correct"). No build branch has ever edited `.ai/flows/`.
  - §6's remark that the flow's "Remaining work list is closed here" describes what the slice delivers. It is not write permission.
  - Remedy: revert the file. The coordinator updates the flow at or after the merge.

- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Order/ReturnCommandHandlers.cs:261-361 (DecideReturnedStockCommandHandler, C24) — Ordering performs quarantine disposition itself, which a closed decision put in Inventory.
  - What the code does:
    - It dispatches Inventory's `PostStockTransferCommand` (restock, :326) and `RequestStockAdjustmentCommand` (write-off, :346) inside its own transaction.
    - It picks the adjustment category.
    - It re-implements BR-3 locally from Inventory's batch facts (:313-318).
  - What was decided:
    - HO-ORD-001 OQ-19 (closed 2026-09-23) and the README C-2 Amended row say "restock-from-quarantine and write-off are Inventory's screens, not this slice's calls … Ordering's responsibility ends at `ReceiveReturnAsync`".
    - HO-IMP-001 §8.8 says the same and records the request as "refused on purpose".
    - Neither command is in CONTRACTS.md or HO-IMP-001's published contract.
    - §14 / §20.2: "A missing port method is §20.2, never a local re-implementation".
  - Remedy: C24 records the decision on the return only, or is removed. Disposition stays on Inventory's quarantine screen. The §11.4 C24 row and its test rows need the matching Amended row.

- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Order/OrderStockFulfilment.cs:25-51,74-107; OrderFulfilmentCommandHandlers.cs:51-52,79,93-94,119,342; ReturnCommandHandlers.cs:74,138,271-307 — Ordering's handlers consume Inventory's internal ports `IStockPositionReader` and `IInventoryClock`.
  - The interface's own doc says it is "for Inventory's own handlers and for EfStockLedger". HO-IMP-001's Amended row calls it "Inventory-internal".
  - No other area consumes it. CONTRACTS.md publishes IStockLedger only.
  - Uses:
    - BR-14 at dispatch and ready-for-collection (GetBatchFacts plus the inventory clock).
    - Reallocation, where Ordering reads FEFO candidates and computes the per-location split for ExplicitLines. This is batch/location selection in Ordering (§4.4, §20.5).
    - BR-10 eligibility at return request.
    - C24.
  - The published contract already enforces the dispatch half: IStockLedger rule 7 refuses to consume a reserved batch that is recalled or expired. The Amended row (3) misdescribes the read as "Inventory's `Batches`".
  - Remedy: rely on the ledger's refusal for BR-14, or have HO-IMP-001 publish a batch-status read (the `IBatchStatusQuery` §8.14 assumed) through its own Amended row. Reallocation should pass only the staff-chosen batch and let Inventory choose locations.

- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Order/IssueRefundCommandHandler.cs:34-36,118-121,149-172; OrderBilling.cs:132-140; OrderPaymentCommandHandlers.cs:105,195 — Ordering reaches into Billing's internals.
  - The refund handler injects `IAggregateRootRepository<InvoiceAggregate>`, `IAggregateRootRepository<PaymentAggregate>` and Billing's `IBillingFinder`.
    - It loads Billing aggregates tracked into Ordering's unit of work.
    - It reads invoice lines, gross amounts, credited-so-far and `payment.Unallocated` to plan Billing's credit note and refund allocation.
    - IBillingFinder is documented as "the cross-record lookups a Billing command handler needs".
  - The held-payment approval dispatches Billing's `ApprovePaymentCommand`, a Billing-internal approver command (HO-BIL-001 C11) that §8.13 does not publish.
    - It records the payment under a fabricated `UserContext(held.RecordedBy, …) { Roles = <approver's roles> }` (:195) so that Billing's maker-checker accepts it.
  - §8.13's published surface is the charge recorder, the four commands, the events and three queries (GetSourceBillingStatusQuery among them). None of these internals is in it.
  - Remedy: use the published surface (let `IssueCreditNoteCommand`/`IssueRefundCommand` do the allocation, or get a published planning query), or obtain a HO-BIL-001 Amended row publishing what is needed. Refer the impersonated recorder to SECURITY.

- [HIGH] src/Domain/CatEcommerce.Domain/DomainServices/ITransactionalScope.cs, src/Infrastructure/CatEcommerce.Infrastructure/Ordering/TransactionalScope.cs:27-74, OrderBilling.cs:30,74,95; .ai/decisions/adr.md ADR-215; HO-ORD-001 §1 Amended (build) (1) — a self-approved cross-context transaction model that contradicts the published Billing contract and an Active Billing ADR. This needs an architect/owner decision before it is copied.
  - §20.2 names exactly this case ("if … Billing's invoice issue commits on its own instead of staging … report it"). It says the fix is an Amended row "here (and, if needed, there)". There is no HO-BIL-001 row.
  - HO-BIL-001 §8.13 publishes a different producer sequencing: charges enlisted, then the producer dispatches the invoice and payment after commit, with unbilled charges as the recovery.
  - ADR-215 instead runs Billing's own handlers inside an Ordering-opened transaction.
    - It switches EF auto-savepoints off for them. This disables Billing's ADR-179 numbering retry.
    - It takes a `pg_advisory_xact_lock("billing-numbering:{branch}")` to serialise Billing's series. ADR-179 says "No PostgreSQL sequence, lock or explicit transaction", and ADR-099 likewise forbids advisory locks for stock.
    - It depends on Billing's frozen-invoice trigger internals (`xmin = pg_current_xact_id()`).
  - The ICustomerHoldSerializer precedent serialises within one context. It does not wrap another context's commands.
  - The pattern already serves C9, C11, C12, C14, C18, C19, C25 and C26, and BRD and SUB would copy it.
  - Remedy: decide ADR-215 explicitly, with Billing's agreement recorded in a HO-BIL-001 Amended row and ADR-179 amended for callers inside a scope. Otherwise revert to §8.13's sequencing and its reconciliation path.
  - TEST: a staff-issued invoice racing an Ordering-issued one at the same branch, under real concurrency.

- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Order/CompleteCounterSaleCommandHandler.cs:20-47; .ai/decisions/adr.md ADR-216 — recorded `Status: Active` although the Amended row says "Owner decision requested".
  - It should be Proposed until the owner rules.
  - The design is an Account record with a fixed GUID, created from Ordering outside the sale's transaction, with a squattable reserved mobile (SECURITY backlog 15/21).
  - Owner question OQ-14.
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Ordering/CatalogueLookup.cs:24-79 — reads Shop's internal Infrastructure projections (`SkuRows`, `PriceRows`, `BundleComponentRows`) and `db.Products`.
  - This is compliant on the ADR-111 / CatalogueSkuDirectory precedent, because Shop's contract queries live in Read and cannot be reached from the write side.
  - But Amended (3)'s claim that Shop "published projections, not ports" is inaccurate. HO-SHP-001 §8.12 publishes GetSkuSaleFactsQuery, FindSkuByBarcodeQuery, GetBundleCompositionQuery and CheckPrescriptionEligibilityQuery for HO-ORD-001.
  - Record the adapter in an ADR the way ADR-111 does.
- [MEDIUM] src/Api/CatEcommerce.Api/Modules/OrdersModule.cs:45 — registers the host-wide `IDbContextFactory<CatEcommerceDbContext>` as a Singleton OpsDbContextFactory with no audit interceptors.
  - Any later area that resolves the factory silently gets an unaudited context.
  - Use a dedicated interface (for example, `IOpsDbContextFactory`).
- [MEDIUM] src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs — §12.5's `ix_sales_order_customer_idempotency → 409 DuplicateSubmission` mapping is absent.
  - OrderConfiguration.cs:17 claims it, but a violation reaches the filter as an unknown constraint and answers 500.
- [MEDIUM] scope — 26 files fall outside §13's literal list and are not covered by ADR-341's partial-file pattern (list below).
  - All are additive, and several are declared in the Amended row.
  - The seventh migration `AddOrderingCounterChange` exceeds "the six migrations of §12.1 only".
  - `tests/CatEcommerce.Infrastructure.Tests/**` is not a §13 test path.
- [MEDIUM] tests/CatEcommerce.Domain.Tests/Common/BillingConventionTests.cs:139 — Billing's C-5 convention test was narrowed from the whole Domain assembly to Billing namespaces, so that Ordering's `IWebhookInbox`/`InboundWebhookReceipt` pass.
  - The no-Gateway/PaymentToken/SavedCard scan now has a hole for every namespace that is neither Billing nor Order. The Ordering test scans Order types only.
  - Narrow the pattern (exempt the inbound-webhook types by name), not the scope.
- [MEDIUM] docs/handover/HO-ORD-001.md:19 — the Amended (build) row omits or misstates several cross-context dependencies:
  - IStockPositionReader/IInventoryClock (stated as "Batches");
  - Inventory's PostStockTransfer/RequestStockAdjustment commands;
  - Billing's ApprovePaymentCommand, InvoiceAggregate/PaymentAggregate repositories and IBillingFinder.
  - A reviewer relying on the row would miss three of the HIGHs above.
- [LOW] src/Domain/CatEcommerce.Domain/Events/Order/OrderEvents.cs — `RefundIssuedEvent` has the same simple name as Billing's published `RefundIssuedEvent` integration event. A handler with the wrong `using` subscribes to the wrong one.
- [LOW] §8.14 — `ICourierGateway` (owned port, "declared for the Q-4 adapter", in §13) is not declared. Acceptance row A27.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Order/OrderBilling.cs:1, IssueRefundCommandHandler.cs:1 — the result records of Billing's published commands (IssuedInvoiceResult, RecordPaymentResult, IssuedCreditNoteResult) live in Billing's handler files (`Application.CommandHandlers.Billing`).
  - Consuming them is unavoidable, but it couples Ordering to handler files. Billing should move them next to the commands.

NOTES FOR TEST
- ADR-215 under real concurrency: a staff-issued and an Ordering-issued invoice for the same branch and year.
- Nested dispatch wraps handler exceptions in TargetInvocationException, and ADR-205 unwraps them. Confirm the 5xx and the reconciliation-issue path end to end.
- Append-only triggers on order_status_change and order_payment_ref: confirm that no tracked save (merge reassign, anonymisation redaction, refund) emits an UPDATE on those rows.
- §20.3 applies to the I-D9/I-D10 tolerance changes in 900c4c5a.

SCOPE CHECK
- files in allowed scope: 256/283. Files counted in scope:
  - §13 paths, the ADR-341 partials (OrdersModule.cs, CatEcommerceDbContext.Ordering.cs, Actions/ActionRoles/BusinessViolationCodes/SettingDefinitions/SettingKeys/NotificationTemplateCatalogue/RecordClassification `.Orders.cs`);
  - the §1 Amended row in docs/handover/HO-ORD-001.md (by convention);
  - ADR-215/216 (reserved by §9);
  - other areas' unit-test fakes gaining `OrderLink`.
- files outside allowed scope:
  - §14 forbidden (BLOCKER): `.ai/flows/order-fulfilment-flow.md`
  - outside §13 (MEDIUM; the declared ones are marked *):
    - Shared: `Constants/SystemActors.cs`, `Helpers/UtcInstant.cs`
    - Infrastructure: `Configuration/RateLimitingOptions.cs` (HO-ACC-001 file, append), `Configuration/CouriersOptions.cs`, `Configuration/OrdersOptions.cs`
    - Api: `Controllers/ReturnControllerSupport.cs`, `StaffOrderControllerBase.cs`, `StaffOrderReconciliationController.cs` (§13 names StaffReconciliation and StaffRefunds), `Filters/ConflictAsQuoteChangedAttribute.cs`*, `Ordering/OrderingResults.cs`
    - Domain/DomainServices: `ICartFinder`, `ICatalogueLookup`*, `IIdempotencyStore`, `IOrderBillingReader`*, `IOrderFinder`*, `IOrderNumberGenerator`, `IReturnFinder`*, `ITransactionalScope`*, `IWebhookInbox`*, `RefundPolicies` (holds RefundCalculator/RefundMethodPolicy), `TenderCalculator`*
    - Domain/Entities: `IdempotencyRecord.cs`, `InboundWebhookReceipt.cs` (named in §8.1)
    - Migrations: `20261009125141_AddOrderingCounterChange.cs` and `.Designer.cs`*
    - Tests: `tests/CatEcommerce.Infrastructure.Tests/Ordering/CurrencyStartupGuardTests.cs`
- new NuGet/npm dependencies: none.

---

## RE-CHECK 1 — 2026-10-10

GATE: ARCHITECTURE
HANDOVER: HO-ORD-001
COMMITS: cfb8aea9 cff232bd 31d03485 (ho/ORD-001, head 31d03485; the merge dc0e4e1f brings in master only and was not re-reviewed), plus the uncommitted Amended row in docs/handover/HO-ORD-002.md, committed with this record
DATE: 2026-10-10
STATUS: PASS

What this re-check did:
- Read the new Amended rows: HO-ORD-001 (gate fix 1-10), HO-BIL-001 (HO-ORD-001 gate fix), HO-IMP-001 (HO-ORD-001 gate fix), CONTRACTS.md rows 1, 2 and 24-26, and the HO-ORD-002 row.
- Read the ADR-179 amendment and the rewritten ADR-215 and ADR-216.
- Read every source hunk in cff232bd (the migration Designer was skimmed; the snapshot diff is Ordering-only).
- Grepped the Ordering handlers, helpers, Infrastructure/Ordering and the Ordering controllers at HEAD for IStockPositionReader, IInventoryClock, IBillingFinder, InvoiceAggregate, PaymentAggregate, `new UserContext(`, PostStockTransferCommand, RequestStockAdjustmentCommand, pg_advisory, SerialiseAsync and the walk-in constants. There are no hits.
- Scanned the fix diff for residue of the superseded architecture: IDispatcher, Bus, Mongo, Bson, QueuePublishOnly, 202, CommandServices, IReadRepository and csproj edits. There are none.
- Owner decisions, as relayed by the coordinator and recorded in the Amended rows and ADR status lines:
  - 2: published IStockLedger members.
  - 3: C24 removed.
  - 4: Billing's published refund planner and payment approval.
  - 5: ADR-215 accepted; Billing publishes the transaction entry point; amend ADR-179 and HO-BIL-001.
  - 6: ADR-216 replaced.
  - 7: weight bands.
  - 8: branch-scoped reconciliation.
  - 9: the cheap MEDIUMs.
  - 10: cart reminders deferred.
- No build, test run or database was used. Runtime points are under NOTES FOR TEST.

EARLIER FINDINGS — status
- [BLOCKER] `.ai/flows/order-fulfilment-flow.md`: **CLOSED.** cfb8aea9 restores the master content. `git diff master HEAD -- .ai/` now shows only additions to `decisions/adr.md`, with no deletion.
- [HIGH] C24 quarantine disposition in Ordering: **CLOSED.**
  - Removed: DecideReturnedStockCommand, its handler and validator, the request DTOs, the route, Returns.DecideStock, ReturnStockDecision, StockDecisionReasonRequired, ReturnedStockDecidedEvent with its audit subscription, the ReturnLine decision fields, and `ReturnRequestAggregate.Decide`.
  - The four `return_line` columns are dropped in migration OrderingGateFixes.
  - No Inventory command is dispatched from Ordering any more. Ordering's responsibility ends at ReceiveReturnAsync, as OQ-19 and HO-IMP-001 §8.8 say.
- [HIGH] Inventory internals (IStockPositionReader, IInventoryClock): **CLOSED.**
  - BR-14 (CheckBatchesAsync), BR-10 (RequestReturn) and reallocation now use only `IStockLedger.GetBatchUsabilityAsync` and `ReserveBatchAsync`.
  - Ordering passes the staff-chosen batch and the reason. Inventory picks the locations (`EfStockLedger.Consumer.cs` ReserveBatchAsync) and then runs its own explicit-plan path.
  - Both members are published through HO-IMP-001's Amended row and CONTRACTS rows 1 and 2. They are additive, and FakeStockLedger answers them fail-closed.
  - `OrderingPublishedPortsOnlyTests` guards the rule by reflection.
- [HIGH] Billing internals and the synthesised UserContext: **CLOSED.**
  - IssueRefundCommandHandler injects IBillingRefundPlanner. Billing's read-only AsNoTracking port lives in Infrastructure/Billing and is published as CONTRACTS row 25 (now #27).
  - Ordering no longer injects Billing's aggregate repositories or IBillingFinder. IBillingFinder stays Billing-internal, behind the planner.
  - The held transfer now records the Billing payment as the real first person when it is held. The real second person approves through Billing's ApprovePaymentCommand, or rejects through RejectPaymentCommand. Both are published as CONTRACTS row 26 (now #28). Billing enforces maker-checker itself.
  - The `new UserContext(held.RecordedBy, ...)` is gone, and `Orders.ApprovePayment` is aligned to Billing's Payments.Approve roles.
- [HIGH] Self-approved cross-context transaction (ADR-215): **CLOSED by owner decision 5.**
  - ADR-215 is "Accepted by the owner on 2026-10-09".
  - Billing now owns and publishes the entry point, IBillingTransactionScope (BillingTransactionScope, registered Scoped in BillingModule). It owns the savepoint rule and the per-branch numbering lock (`DocumentNumbering.LockSeriesRowAsync` takes `FOR UPDATE` on the series row only when a transaction is open, and skips the in-process gate there).
  - ADR-179 carries an appended "Amended 2026-10-09" note, and HO-BIL-001 §8.13 has the matching Amended row and sequencing paragraph.
  - Ordering's ITransactionalScope is now `RunAsync` and `FlushAsync` only: it delegates to Billing's scope and adds the 23514 -> StockNoLongerAvailable translation. SerialiseAsync, NumberingLock and SerialiseCreditNotesAsync are deleted.
  - The precedent for BRD and SUB is now a Billing-published port, not an Ordering pattern.
- Earlier MEDIUMs:
  - M1 (ADR-216 status): resolved. The ADR was rewritten and accepted.
  - M4 (idempotency 409 mapping): resolved. The mapping is in `UniqueConstraintExceptionFilter.cs:128`.
  - M7 (Amended row misstated dependencies): resolved by gate fix 2 and 4.
  - M2 (CatalogueLookup ADR), M3 (OpsDbContextFactory), M5 (scope) and M6 (BillingConventionTests) remain open as backlog ORD 23, 24, 26 and 27.
  - L1-L3 remain open as backlog ORD 29-31.

NEW IN THE FIX DIFF — checked and compliant
- Layers:
  - The new Domain ports (IBillingTransactionScope, IBillingRefundPlanner, ICounterCustomerFinder) and DTOs (StockBatchModels, BillingRefundPlanModels) reference no EF, Application or Read type.
  - Application still does not reference Read.
  - The ACC lookup adapter `Api/Ordering/AccountsCounterCustomerFinder.cs` dispatches HO-ACC-001's Q-H2 FindCustomersQuery through IInMemoryDispatcher via GetResultAsync. The Api layer already dispatches queries from RequireActionFilter and the work cycles, so this is layer-legal.
- Counter sale (ADR-216, accepted):
  - It uses HO-ACC-001's own RegisterWalkInCustomerCommand unchanged, inside the sale's transaction. This is the same multi-aggregate unit ADR-215 accepts for Billing.
  - No Account code is edited. The shared walk-in account and its reserved number are deleted.
- Controllers: still one dispatch per action, branch from IActiveBranchAccessor, ValidateCommand stamped. Reconciliation list and resolve are branch-scoped, and GetReconciliationIssuesQuery filters on BranchId with AsNoTracking and paging.
- Registration:
  - IBillingTransactionScope, IBillingRefundPlanner and ICounterCustomerFinder are Scoped.
  - The removed C24 handler, validator and event registrations are gone with their types.
  - Nothing that AddInMemoryBusServices registers is hand-registered.
- Persistence:
  - New columns `held_payment_id` and `reconciliation_issue.branch_id` are mapped in the IEntityTypeConfiguration. The backfill is schema-local and Down works.
  - The snapshot diff touches only Ordering. No Billing or Inventory table is written by Ordering code: the planner and the usability read are AsNoTracking reads inside the owning area's own implementation.
- Errors: DeliveryTaxClassNotConfigured is a DomainRuleViolationException mapped to a 400 CommandResponse, not a throw-for-5xx. Benign concurrency or cancellation no longer opens a reconciliation issue. DbUpdateConcurrencyException is recognised by type name, because Application does not reference EF.
- Weight bands: CheckoutPricing (a pure domain service) sums quantity x ShippingWeightGrams. CatalogueLookup still passes null with a TODO tied to HO-SHP-001-FU3. Shop is not edited.
- HO-ORD-002 Amended row (uncommitted until this record): it describes contract changes (1)-(6), each of which matches the code: no decisions route, `customerName` and `customerMobile` on the POS sale, ApprovePayment roles and pay-at-hold, DeliveryTaxClassNotConfigured 400, weight bands, branch-scoped issues.

FINDINGS
- [MEDIUM] `.ai/decisions/adr.md:3770` (ADR-204, Active) still says "Billing is written only when final". C11 now records the Billing payment (PendingApproval) and issues the invoice at hold time (`OrderPaymentCommandHandlers.cs` TransferConfirmation.FinaliseAsync, `hold` branch).
  - ADR-204's stated reason was to avoid needing a Billing->Order event. The new design reopens exactly that gap (backlog ORD 33).
  - ADR-204 is in this slice's own range. Append an "Amended 2026-10-09 (owner decision 4)" note so the Active ADR does not contradict the code.
  - Backlog ORD 37.
- [MEDIUM] Scope: 8 fix-round files are outside §13's literal list (and outside §14). Three were already on the earlier list (ReturnControllerSupport, StaffOrderReconciliationController, ITransactionalScope). The new ones:
  - `src/Domain/CatEcommerce.Domain/DomainServices/ICounterCustomerFinder.cs` and `src/Api/CatEcommerce.Api/Ordering/AccountsCounterCustomerFinder.cs`. §13 names only GuestCartCookieWriter in `Api/Ordering`. This is the first Domain port implemented in the Api project; declare it in the Amended row.
  - `src/Api/CatEcommerce.Api/Filters/ConcurrencyExceptionFilter.cs:22-26`. §13 allows "the placement-route override only". The TargetInvocationException unwrap is host-wide. It is declared in gate fix 9(b) and is harmless, but it is an HO-ACC-001 file.
  - `Migrations/20261009165036_OrderingGateFixes.cs` and `.Designer.cs`. This is an eighth migration, beyond "the six of §12.1"; the integrator regenerates it.
  - Backlog ORD 38.
- [LOW] `src/Application/CatEcommerce.Application/CommandHandlers/Validators/OrderPaymentCommandValidators.cs:75`. ResolveReconciliationIssueCommandValidator has no `BranchId NotEmpty` for the new required field. `Guid.Empty` would only ever answer 404, so this is cosmetic. Backlog ORD 39.
- [LOW] Merge-order risk. This branch edits HO-IMP-001's `IStockLedger.cs`, `EfStockLedger.Consumer.cs` and `FakeStockLedger.cs`. HO-PHM-001 is in progress and adds its own members to the same interface and fake (the HO-IMP-001 Amended row says so). The integrator merges them additively and keeps both sets. Backlog ORD 40.

SCOPE NOTE — §14 files changed under recorded owner decisions (not counted as violations)
- §14 forbids "Every file of HO-IMP-001 ... HO-BIL-001" and `docs/handover/**`. The fix round changes 14 such files:
  - `IStockLedger.cs`, `EfStockLedger.Consumer.cs`, `FakeStockLedger.cs`;
  - `BillingModule.cs`, `DocumentNumbering.cs`, `BillingTransactionScope.cs` (new), `EfBillingRefundPlanner.cs` (new), `IBillingTransactionScope.cs` (new), `IBillingRefundPlanner.cs` (new);
  - `docs/handover/{CONTRACTS,HO-BIL-001,HO-IMP-001,HO-ORD-001,HO-ORD-002}.md`.
- This gate accepts them because:
  - each is the remedy the first ARCHITECTURE run prescribed under §20.2 ("an Amended row here (and, if needed, there)");
  - each is recorded as an owner decision in the owning handover's Amended row and in CONTRACTS.md;
  - all of them are additive (new members, new files, a lock taken only inside an open transaction). No published member is renamed or removed.
- The ADR-179 amendment is an appended note (the owner directed "amend ADR-179"). The HO-ORD-001 body edits are strikethroughs with a pointer to the Amended rows, which has precedent in 9 other handovers.
- **This PASS rests on that owner authorisation.** The only evidence of it is the branch-authored Amended rows, the ADR status lines and the coordinator's brief; there is no separate owner-signed record. If the owner did not authorise Billing and Inventory code being changed from this branch (as opposed to by those areas), these 14 files are §14 BLOCKERs. The integrator should confirm this at merge.

NOTES FOR TEST
- Counter sale, new buyer: RegisterWalkInCustomerCommand runs inside the scope with savepoints off, and `ix_customer_account_mobile_number` is unique across all statuses. Two problems follow:
  - Two concurrent sales with a new mobile, or a mobile held by a non-Active account, hit a 23505 that aborts the whole transaction. The "raced -> find" fallback in `CompleteCounterSaleCommandHandler.RegisterBuyerAsync` therefore cannot run. Expect a 4xx/5xx plus a reconciliation issue, not a found buyer. Verify this and judge it.
  - AccountsCounterCustomerFinder dispatches Q-H2 as the cashier. Confirm that Reception gets a result: the query is behind `Customers.View` at the HTTP edge.
- ADR-179 amended: a staff-issued invoice outside any transaction races a scoped one at the same branch and year. The unscoped side must block on the `FOR UPDATE` row and then retry on xmin. `OrderingBillingNumberingEndpointTests` claims this; confirm it under real concurrency and that nothing deadlocks across invoice and credit-note kinds.
- C12 approve and reject: Billing refusals (ApproverMustDiffer, PaymentNotPending) answer 4xx and roll back the order change. A hold made before held_payment_id existed is refused with NoPaymentAwaitingApproval.
- Refund planner: the draw plan is read after IssueCreditNoteCommand flushes inside the same transaction. Confirm that the released excess is visible to `PlanRefundAsync`.

SCOPE CHECK
- Files in the fix round: 92 (3 commits plus HO-ORD-002.md).
- In allowed scope: 70/92. This covers:
  - §13 paths;
  - the ADR-341 partials;
  - `tests/**`;
  - `docs/test-cases/TC-HO-ORD-001.md`;
  - `docs/backlog/ORD.md` (by the gate convention);
  - `.ai/decisions/adr.md` (ADR-215 and ADR-216 in §9's range, plus the owner-directed ADR-179 note);
  - the `.ai/flows` revert (net zero against master);
  - the new Models DTOs.
- In §14 paths, accepted under recorded owner decisions: 14 (listed in the scope note above).
- Outside §13 (MEDIUM): 8. These are ICounterCustomerFinder, AccountsCounterCustomerFinder, ConcurrencyExceptionFilter (beyond its line restriction), OrderingGateFixes (.cs and .Designer.cs), ReturnControllerSupport, StaffOrderReconciliationController and ITransactionalScope.
- New NuGet or npm dependencies: none. No csproj, sln, NuGet.Config or global.json change.

- Delta review (post-TEST fixes + master merge): see docs/gates/HO-ORD-001-CODE_REVIEW.md, section "Delta review (post-TEST fixes + master merge)" - PASS, no BLOCKER/HIGH.
