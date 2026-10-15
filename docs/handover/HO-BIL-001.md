# HO-BIL-001 — Billing and Payments (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-BIL-001** |
| Title | Billing and Payments — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §12 — **US-BIL-01 … US-BIL-12** (all twelve: 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12). §18.5 (gateway deferrals) is **forbidden scope** here |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-BIL-1 … FR-BIL-13**, FR-ORD-5 (total formula), FR-ORD-17 (refund record), FR-ORD-19 (combined invoice), FR-ORD-21 (COD collection record), FR-BRD-7 (deposit), FR-BRD-15 / FR-SRG-9 (charges to the stay invoice), **C-5**, **C-6**, C-7, C-9, **A-2**, A-4, BR-11, **BR-17**, D-5, D-6, D-9, D-12, NFR-LEG-3, NFR-SEC-2, NFR-SEC-5, AC-8 (reconciliation half), AC-16, §3.3 (accounting export), §8.2 (financial reports — data only), §10 Phase 5 (deferred), Appendix C (invoice states), Appendix D ("Invoices & payments", "Refunds & credit notes") |
| Pairs with | [`HO-BIL-002`](HO-BIL-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (customers, staff, sessions, `ActionRoles`, audit capture, `CustomerMergedEvent`), [`HO-ADM-001`](HO-ADM-001.md) (branches, **`Money`**, services, prices, **tax classes and effective-dated rates**, settings registry, time model), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, link builder), [`HO-APT-001`](HO-APT-001.md) (`AppointmentCompletedEvent`, `ServiceSnapshot`) |
| Consumed by (contracts published here) | **HO-ORD-001** (order charges, COD collection, transfer confirmation, invoice at confirmation, refunds), **HO-BRD-001** (stay charges, deposits, cycle invoices, spending-limit totals), **HO-EMR-001**, **HO-PHM-001**, **HO-SRG-001** (billable items — assumed contract §8.13), HO-SUB-001 (via orders), HO-POR-001 (balances), HO-RPT-001 (tables) |
| Approved design | **None.** No `architect` design exists for Billing. This handover operationalises the SRS, the owner's instruction that there is **no payment gateway in v1**, and the settled upstream handovers. Every decision not covered by an existing ADR is proposed in §9 and every genuine fork is an open question in §9.1 |
| Delivery order | Section 11 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-10-07 — integrator, recorded at the merge of `ho/BIL-001` (`0442f9ff`) from `docs/gates/HO-BIL-001-*.md` (ARCHITECTURE and SECURITY failed round 1 on three HIGHs; REVIEW-R2 closed them; CODE_REVIEW, REVIEW-R2, TEST and ACCEPTANCE: PASS) and `docs/test-cases/TC-HO-BIL-001.md` (158 rows at the TEST gate: 147 PASS, 7 FAIL — five TEST-gate probes and two regression rows restating then-unfixed findings — 4 DEFERRED; the ACCEPTANCE gate re-ran the Billing integration classes at `d974c14b`: 146 passed, 2 skipped, XI-7 and XI-15, and the probes XU-3, XU-6 and XI-8 now pass). Medium/low findings are in [`docs/backlog/BIL.md`](../backlog/BIL.md).** (1) **Phase J is deferred by owner decision** (§17): the Visit, Dispense and Surgery item sources are neither built nor stubbed, no handler for the assumed `VisitFinalisedEvent`, `PrescriptionDispensedEvent` or `SurgeryCompletedEvent` exists, and an unsupported kind is refused with `SourceKindNotCapturable` (the seam is `IBillableItemSource` / `BillableItemSourceResolver`, marked at `BillingModule.cs:51`). §8.13's "assumed" table is therefore unchanged and still unverified. **HO-EMR-001 has since merged:** when Phase J is picked up, §20.2 requires its real event to be compared with the assumed `VisitFinalisedEvent` row before anything is built (backlog BIL 32). (2) **The developer's 18 declared deviations, as the ARCHITECTURE gate judged them** (all accepted; none reverses an ADR): (a) `IBillingFinder` / `EfBillingFinder`, a Domain port of `AsNoTracking` id and value lookups (the `IFlaggableAppointmentFinder` precedent); handlers still load the aggregates they mutate through `IAggregateRootRepository`. (b) The receipt number is taken before the save with `nextval('billing.receipt_number_seq')` (`EfBillingFinder.cs`); gaps are allowed for receipts (§8.8) and ADR-179 covers document numbers only. (c) Document numbering retries up to 5 times on a unique violation of `ix_invoice_number` / `ix_invoice_sequence` / `ix_credit_note_number` / `ix_credit_note_sequence` (the same stale-series race, seen when EF sends the document INSERT before the series UPDATE), reloading only the series row, and a static in-process `SemaphoreSlim` per (branch, financial year, kind) (`DocumentNumbering.cs`) reduces collisions; the gate is an optimisation, not a lock, and correctness rests on `xmin` and the unique indexes across processes (proved by the TEST gate's XI-1 / XI-1b with two hosts). (d) `BillingIdempotencyReplayFilter` is a second, global exception filter added to `MvcOptions` from `BillingModule.cs:57`, placed after `UniqueConstraintExceptionFilter` with equal order so it sees the exception first (XI-2), not an extension of that filter as §12.5 says; it resolves `IBillingFinder` directly and does not check the stored record against the replayed request (backlog BIL 10). (e) The manual charge is built by its own handler, not the recorder. (f) The `loyalty_entry` unique indexes are filtered on `idempotent_source` (the literal §12.1 index on `(kind, invoice_id)` would block redeem, remove, redeem again on one draft). (g) Merge transfer entries carry the duplicate's id as `SourceCustomerId`, one Out and one In entry per duplicate. (h) Extra ports and services: `CodShortfall` and `LoyaltyCalculator` (pure), `LoyaltyDependencies` (a parameter record), `ILoyaltyAccountStore`, `IBankMatchRecorder` (it saves outside the repository, so `BankLineMatchedEvent` is never dispatched; backlog BIL 12). (i) The credit-note excess release is posted in the same save (§8.9 step 4, ADR-184). (j) `PaymentActionRules` lives in `InvoiceActionRules.cs`. (k) Extra no-delete triggers beyond the immutability list ("No Billing table accepts DELETE", D-6). (l) The cash-up id is deterministic per branch and day; a concurrent first count surfaces as an unmapped PK violation because `cash_up` is not in `BillingFirstWriteTables` (backlog BIL 17). (m) **A 60-day window for late items** (`CashUpRows.LateItemWindowDays`) bounds ADR-188's "never lost" (backlog BIL 14, accepted by the owner). (n) **The accounting export is built in memory** up to `MaxExportRows` (200,000), not streamed, because the SHA-256 is recorded first (backlog BIL 15, accepted). (o) CSV row counting (a CODE_REVIEW concern; no finding). (p) Extra violation code `StatementNotFound`; also undeclared: `SourceKindNotCapturable`, `SourceNotFound`, `BankMatchNotFound`, `ServiceNotPriced` (the codes file is append-only). (q) Per-area partials under ADR-341: `BillingModule`, `CatEcommerceDbContext.Billing`, and `Actions` / `ActionRoles` / `BusinessViolationCodes` / `SettingDefinitions` / `SettingKeys` `.Billing`. (r) No `.ai/flows` updates (§14 forbids `.ai/**` other than `decisions/adr.md`). **Thirteen files outside §13's literal list**, all in the Billing area and none in §14 forbidden scope: `src/Api/CatEcommerce.Api/Controllers/StaffBillingControllerBase.cs`; `…/Controllers/StaffCashUpController.cs` (§13: `StaffCashUpsController`); `…/Controllers/StaffLoyaltyController.cs`; `…/Controllers/StaffReconciliationController.cs` (§13 names `StaffCodReconciliation` and `StaffBankReconciliation`; `StaffBillingCustomersController` is also absent, its routes live elsewhere); `…/Filters/BillingIdempotencyReplayFilter.cs`; `src/Domain/CatEcommerce.Domain/DomainServices/CodShortfall.cs`, `IBankMatchRecorder.cs`, `IBillingFinder.cs`, `ILoyaltyAccountStore.cs` and `LoyaltyCalculator.cs`; `tests/CatEcommerce.Infrastructure.Tests/Billing/AccountingExportWriterTests.cs`, `…/Billing/BankStatementCsvParserTests.cs` and `…/Persistence/BillingMigrationSqlTests.cs`. The per-area partials, `RecordClassification.Billing.cs` (C-ALL-1), the nine grouped `Billing*CommandValidators.cs` files (§13 says one validator file per command) and the test-infrastructure files `BillingApiFactory` / `BillingLoggedApiFactory` were counted in scope. (3) **Gate fix rounds** (`HO-BIL-001-REVIEW-R2.md` for round 1; the later commits `ede9f7d8` and `d974c14b`, which ACCEPTANCE delta-reviewed): (a) **`CardNumberGuard` now applies to every free-text money field** through `BillingFreeText.RefuseCardNumber` — payment Reject / ReleaseEarmark / Reverse reasons, the earmark label, credit-note reason, cash-up explanation, bank-match unmatch reason, charge void reason, manual-charge description, corrected-draft line description and invoice notes (round 1), then the billing profile's legal name, address lines, phone, email, footer, bank name, account name and branch name, charge `ReportingTag`, statement `BankAccountLabel`, and the courier code and name on a COD payment (`d974c14b`); a refused value is never echoed or logged (the violation carries a constant message and the property name). The guard was hardened: it normalises before matching, so double space, spaced dashes, tab, NBSP, `.`, `/`, `_`, zero-width space, soft hyphen, Unicode hyphens and dashes, `, : ; | * ( ) [ ]`, a stray digit glued to the front, Bengali, full-width and supplementary-plane digits are all treated as separators or digits; bank-statement text is masked before it is truncated, and `BankTransactionId` is masked too. (b) **Two exemptions narrow §8.8.2's literal rule** ("a run of 13-19 digits passing Luhn → true"): a Bangladeshi mobile number with its country code (`^8801[3-9]\d{8}$`, applied only to a window of exactly 13 digits, so a longer card window next to a mobile is still caught) and the system's own document numbers (`INV-` or `CN-`, a branch code, two consecutive two-digit financial years and `\b`-bounded six-digit sequences, at most 14 digits, so it cannot hide a 15-19 digit card; `INV-A422-2222-222222` is still refused). Without them the guard refused `bKash 8801712345605` and `INV-DHK001-2627-000123` about one time in ten (TEST F-5). (c) **Letters deliberately do not join digit groups:** `4111x1111x1111x1111` is neither refused nor masked, so the XU-3 letter case moved from "must refuse" to "must not refuse" (`XU_3_ALetterBetweenGroups_DeliberatelyDoesNotJoinThem`); joining letters would refuse ordinary mixed references (backlog BIL 7). (d) **`SourceFileName` is masked rather than refused** (`••••1111.csv`): an uploaded file name is not the user's to choose. Bank account, tax registration and routing numbers stay unguarded on purpose (§11.4 C1). (e) **The read side projects** (ARCHITECTURE HIGH 1): no Billing query handler materialises an aggregate or uses `ToDictionaryAsync` on an entity (89 `AsNoTracking()` statements scanned, none ending in a materialiser without a `Select` / `SelectMany`); ordering is stable (entries by `OccurredOn` then `Id`, ageing by `OldestDueOn` then `CustomerId`); `activePaymentIds` is a correlated `Any`; `U_X6` (`BillingConventionTests.cs`) guards the pattern, narrowly (backlog BIL 22). (f) **The loyalty merge commits in one save** (ARCHITECTURE HIGH 2, ADR-184): `ILoyaltyAccountStore.Stage` adds the `MergeTransferIn` entry to the shared context without saving and `CommitNewAsync` then saves the Out entry, so both accounts and both entries commit together; idempotency is keyed on the Out entry by `SourceCustomerId` (an existing Out implies an existing In). Proved by XI-10 (every account's balance equals its ledger sum). (g) **The accrual clawback is keyed per credit note:** the reversal entry carries `credit_note_id` and a NULL `invoice_id`, `ix_loyalty_entry_kind_invoice` is filtered `idempotent_source AND invoice_id IS NOT NULL`, and `ix_loyalty_entry_kind_credit_note` stops a double clawback, so a second partial credit note on a paid invoice claws points back instead of failing with a `500` (SECURITY MEDIUM; XI-11). No migration was needed. Also: `BillingApiFactory` refuses to start without `CATECOMMERCE_TEST_DB` or when it names `catecommerce_dev`; the TEST gate made `RunDeliveryPassesUntilAsync` throw on exhaustion and fixed the flaky `I_D11`. (4) **The integrator's merge resolutions** (HO-IMP-001 merged first and owns the shared names). (a) **Billing's `DocumentKind` was renamed `BillingDocumentKind`** (`Shared/Common/Enums/BillingDocumentKind.cs`, same values: `Invoice` = 1, `CreditNote` = 2, so the persisted `billing.document_number_series` values and the `INV-` / `CN-` formats are unchanged), because HO-IMP-001 owns `DocumentKind` (`Shared/Common/Enums/DocumentKind.cs`: the inventory document kinds `PurchaseOrder` = 1 … `BatchRecall` = 7; a second type of the same name cannot coexist). Read §8.7 / §10.1 and every `DocumentKind` mention in this document as `BillingDocumentKind`. (b) **Billing's `ToActor` command extension was renamed `ToBillingActor`** (`BillingMapper.cs`; it builds a `BillingActor`), because Inventory's `ToActor` (`InventoryMapper.cs`, an `InventoryActor`) is the same extension on the same type. (c) **`InvalidQuantity` and `ApproverMustDiffer` are declared once, in `BusinessViolationCodes.Inventory.cs`** (same values); `BusinessViolationCodes.Billing.cs` carries a comment at each in place of a second constant. `BillingMessages.ApproverMustDiffer` (the message text) is Billing's own and unchanged. (5) **Owed Amended rows written.** The "Amendments this slice makes to other handovers" row above is now carried out: an Amended row dated 2026-10-07 was appended to HO-ADM-001, HO-NTF-001, HO-ACC-001 and HO-APT-001, each after checking the change exists in `src/` (the nine `billing.*` keys; the three templates and `InvoiceLink` / `ReceiptLink`; the `ActionRoles` rows, the filter mappings and the `CustomerMergedEvent` subscription; the appointment item source and its `AppointmentCompletedEvent` handler). The README ADR ranges (ADR-177…194, ADR-FE-079…086) are not a handover §1 and were not touched here. ADR-177…194 in `.ai/decisions/adr.md` match §9 word for word (ACCEPTANCE: 18 of 18); ADR-195 and ADR-196 are left free. The refinements the build made to ADR-179 and ADR-182 are in the "HO-BIL-001 build amendments" section of `.ai/decisions/adr.md`. (6) **Open after the merge** (tracked in [`docs/backlog/BIL.md`](../backlog/BIL.md)): three owner decisions — the accounting export's branch scope (row 2), record-and-refund by one person (row 3), and loyalty clawback on payment reversal (row 4); the next fixes, in priority order — the card mask picking the wrong digit window, which leaves up to 13 of 16 card digits readable in bank-statement fields (row 1, **top priority**), the unguarded COD remittance `CourierCode` (row 5), TRUNCATE not blocked on billing tables (row 6, a migration, so the integrator's), and the separators and letters the guard still does not join (rows 7 and 8); and Phase J (row 32). *(append further rows here)* |
| Amended (HO-ORD-001 gate fix) | **2026-10-09 -- §8.13 grows by three published items and ADR-179 gains an amendment; owner decisions 4 and 5 of the HO-ORD-001 ARCHITECTURE fix round (ADR-215 accepted by the owner).** (1) **`IBillingTransactionScope`** (`RunAsync`, `FlushAsync`; `Domain/DomainServices/IBillingTransactionScope.cs`, `Infrastructure/Billing/BillingTransactionScope.cs`, registered in `BillingModule`) -- the entry point that lets a producer's own change and Billing's published commands (`IssueInvoiceFromChargesCommand`, `RecordPaymentCommand`, `ApprovePaymentCommand`, `RejectPaymentCommand`, `IssueCreditNoteCommand`, `IssueRefundCommand`) share **one database transaction**. A returned refusal or any exception rolls everything back; a call inside an open transaction joins it. **Billing owns what makes that safe:** EF Core's automatic savepoints are switched off inside the scope (the frozen-invoice trigger accepts a line only for an invoice row written by the top-level transaction, `xmin = pg_current_xact_id()`), and **invoice and credit-note numbering is safe inside it**: `DocumentNumbering.EnsureSeriesAsync` takes the (branch, financial year, kind) series row `FOR UPDATE` before it reads the number when -- and only when -- a transaction is open, and `SaveWithRetryAsync` skips the in-process gate there (a gate holder waiting on a row lock and a row-lock holder waiting on the gate would deadlock). Any other issuer, in a scope or not, waits on that row and then takes the next number. **Producers take no lock, name no series and read no trigger internals** -- HO-ORD-001's earlier `pg_advisory_xact_lock("billing-numbering:{branch}")` is gone. HO-BRD-001 and HO-SUB-001 use this entry point; they write no transaction code. **ADR-179 is amended accordingly** (see its Amended note in `.ai/decisions/adr.md`): outside a transaction nothing changes -- still no sequence, no lock, no explicit transaction, the xmin-guarded series row and the up-to-5 retry. Proved by `OrderingBillingNumberingEndpointTests` (eight scoped counter sales and six staff-issued invoices at one branch, from a cold series, all at once: 14 distinct contiguous numbers). (2) **`ApprovePaymentCommand` and `RejectPaymentCommand` are published** (they were Billing-internal commands, C11). Billing itself enforces maker-checker: the approver or rejecter must differ from the recorder (`ApproverMustDiffer`). The producer records the payment as the **real first person** and the **real second person** approves or rejects through these commands; no `UserContext` is ever copied, built or impersonated (HO-ORD-001 had synthesised one with the approver's roles -- removed). Over `billing.paymentApprovalThreshold` the payment stays `PendingApproval` and the invoice unpaid until then. Role policy stays Billing's: `Payments.Approve` is Accountant and Administrator, and HO-ORD-001's `Orders.ApprovePayment` now has the same two roles. (3) **`IBillingRefundPlanner`** (`PlanCreditAsync(invoiceId, amount)`, `PlanRefundAsync(paymentIdsInOrder, amount)`; `Domain/DomainServices/IBillingRefundPlanner.cs`, `Models/BillingRefundPlanModels.cs`, `Infrastructure/Billing/EfBillingRefundPlanner.cs`) -- a read-only planning port: how an amount is spread over the invoice's remaining credit line by line (a `Full` credit when it is everything left; `InvoiceNotFound` / `CreditExceedsInvoice` otherwise) and which payments it is drawn from, each capped at what the payment still holds unallocated. The producer then dispatches `IssueCreditNoteCommand` / `IssueRefundCommand`. **A producer no longer injects `IAggregateRootRepository<InvoiceAggregate>`, `IAggregateRootRepository<PaymentAggregate>` or `IBillingFinder`** -- `IBillingFinder` stays Billing-internal. Guarded by `OrderingPublishedPortsOnlyTests`. Contracts table: `docs/handover/CONTRACTS.md` rows 26 to 28. **Still open (backlog ORD):** a payment approved directly on Billing's payment list (not through the order) leaves the order's hold in place until its own approve route is used -- a `PaymentAcceptedEvent` consumer would close it; and `IssuedInvoiceResult`, `RecordPaymentResult`, `IssuedCreditNoteResult` still live in Billing's handler files (ORD backlog 31). |
| Amended (HO-ORD-001-FU1) | **2026-10-10 -- additive members on the §8.13 command surface, owner decision D4 and its fix round (`CONTRACTS.md` #29).** `RecordPaymentCommand.ApprovalWaiverReason` (`string?`, default `null`; replaces the first-cut `ApprovalNotRequired` bool): an in-process producer (Ordering) sets `CodCollection` or `CounterTender` and the payment is accepted at once whatever its size, instead of waiting for a second approver above `billing.paymentApprovalThreshold`. **Method restriction:** Billing itself refuses the waiver unless `CodCollection` goes with `CashOnDelivery` or `CounterTender` with `CashInStore` / `CardInStore` -- new violation `ApprovalWaiverNotAllowed` (a bank transfer can never skip approval). **Audit:** the reason is stored on `billing.payment.approval_waiver_reason` (nullable, set only at record time), the payment raises `PaymentApprovalWaivedEvent`, and `AuditBillingDecisionHandler` writes a `BusinessEvent` audit row `PaymentApprovalWaived` (`Reason=..; Method=..; Amount=..`). Not part of the HTTP request (`StaffPaymentsController` does not map it), so BR-17 still applies to every payment staff record in Billing. Also additive: `IssueInvoiceFromChargesCommand.PinnedTaxClassId` / `PinnedTaxRateAsOf` (in-process only; reads that one class's rate as of the instant instead of now). Bank-transfer holds are unchanged. |
| Amended (Phase J) | **2026-10-14 -- OWNER DECISIONS of 2026-10-14 (integrator to review at merge); Phase J built on `ho/BIL-001-J`, lifting the deferral of the 2026-10-07 row and of backlog BIL 32.** (1) **Built:** `VisitBillableItemSource`, `DispenseBillableItemSource`, `SurgeryBillableItemSource` (`Infrastructure/Billing/ItemSources/`), registered in `BillingModule` beside the appointment source, so the existing `CaptureSourceChargesCommand`, `GetUncapturedSourcesQuery` (now also lists finalised visits that have something to bill, dispensed supplies and discharged surgeries with no charge) and `POST .../sources/{kind}/{id}/capture` work for `Visit`, `Dispense` and `Surgery`; four thin handlers (`CaptureChargesOnVisitFinalisedHandler`, `...OnPrescriptionDispensedHandler`, `...OnSurgeryDischargedHandler`, `VoidChargesOnDispensingReversedHandler`) in `DomainEventHandlers/Billing/CaptureChargesOnPhaseJSources.cs`, the Phase B shape (fresh scope, swallow, ids only, never enqueue). `BillableSourceResult` gained `Skipped` (reasons a source could not make an item, merged into the capture result); `IBillingPricingInputs.GetSkuTaxClassIdsAsync` and `IBillingFinder.FindChargeIdsBySourceAsync` were added. **No Billing table, aggregate or migration changed.** (2) **Owner decisions, verbatim choices:** (a) *Who is billed* -- "Cat's owner at finalising": EMR stores `CustomerId` on the visit at finalisation and `VisitFinalisedEvent` carries it (ADR-344; a visit finalised before it existed is not billable, `VisitCustomerNotRecorded`; a merged owner is billed as the survivor). (b) *Diagnostics* -- "Add a price, charge them": each `DiagnosticKind` has an admin-set price, default 0, frozen on the order; one line per non-cancelled order (ADR-345). (c) *Boarding* -- "Skip until Boarding": every visit is its own bill; **deferred to HO-BRD-001 and not stubbed** (no `BoardingStayId` is read, written or invented; a vet charge during a stay is therefore an ordinary visit bill until HO-BRD-001 adds the stay link, so FR-BRD-15 stays open). (3) **Owner's architecture call (backlog EMR 43):** EMR publishes the Domain read port `IVisitBillingReader` with typed values and Billing reads a visit only through it -- not through the Read-layer `GetVisitChargeLinesQuery` and not from `clinic.*`. EMR's kinds map to Billing's in `VisitBillableLineKindMap` (Procedure to Service, Medicine to Medicine, Consumable to Product, Diagnostic to Service), tested. **This differs from this handover's own text**: §8.5 says each source reads "the source area's own table" and §8.13 names `VisitBillableItemSource` "reading the visit's treatments, consumables and diagnostics"; the owner's call was followed for EMR and reported. For PHM and SRG, which publish no typed billing port, §8.5 was followed (untracked projections of the dispensing's and the surgery's own rows, as the appointment source does). (4) **Contract mismatches with §8.13's assumed table (§20.2; each adapted to what is built, nothing invented):** `VisitFinalisedEvent` had no `CustomerId` and has no `BoardingStayId` (CustomerId added by decision 1; the stay is deferred); EMR diagnostics carried no price or tax class (added by decision 2); EMR kinds are Procedure/Medicine/Consumable, not Service/Medicine/Product (mapped); `GetVisitChargeLinesQuery` returns display strings (not used). **There is no `SurgeryCompletedEvent(SurgeryId, AppointmentId, ...)`**: a surgery ends with `SurgeryDischargedEvent(SurgeryId, CatId, BranchId, CustomerId, ServiceId, DischargedOn, PricingBasis, CatWasDeceased)` (no `AppointmentId`, no stay), which is what Billing captures from; the surgery's procedure fee is its own `PriceSnapshot` (campaign price included), **not** "the appointment's service snapshot" (OQ-14's assumption): a surgery's appointment is managed by the surgery and never completes by itself, so the appointment source never bills it and there is no double charge; an anaesthesia-log drug entry carries a SKU and a quantity but **no price**, so it is priced at the SKU price in force when it was given (`ISkuPriceReader.PriceAtAsync`, PHM), and a drug with no price then is skipped and reported `SkuNotPriced`. `PrescriptionDispensedEvent(DispensingId, PrescriptionId, BranchId, CustomerId, CatId, VisitId, SupplyNumber, Lines[])` has no `BoardingStayId` and its lines carry a price snapshot but no tax class (taken from the SKU through Shop's `SkuRows`); the source id is `DispensingId`, not a "DispenseId". `DispensingReversedEvent` is not in §8.13: Billing voids the reversed supply's charges that are not yet invoiced (an invoiced one needs a credit note; a warning with ids is logged). (5) **Rules held:** idempotent per source by `(source_kind, source_id, source_line_ref)` (visit lines `treatment:{id}` / `diagnostic:{id}`, dispense lines `line:{id}`, surgery `procedure` and `drug:{originalEntryId}`), proved for event replay, re-capture, concurrent capture and concurrent finalisation (`BillingPhaseJEndpointTests` J3, J3b, J3c); BDT, tax resolved at issue (0% at launch) and per-line rounding unchanged (the charge records the factors); service date = the branch-local date (Asia/Dhaka) of finalising / dispensing / discharge; the surgery procedure fee is charged only for a procedure that ended `Completed` (an abandoned one bills its drugs only) -- a rule neither handover states, owner question. (6) **Cross-area edits** (owner-authorised, additive; recorded in HO-EMR-001 §1 Amended 2026-10-14): `VisitAggregate.CustomerId`, `VisitFinalisedEvent.CustomerId`, `IVisitBillingReader`, diagnostic price and tax class on `DiagnosticOrderAggregate`, five `clinical.diagnosticPrice*` settings; migration `AddVisitBillingFacts` (three nullable columns, no hand-written SQL needed; the integrator regenerates). PHM and SRG files are untouched. CONTRACTS rows 36 to 39; ADR-344 to ADR-346. (7) **Tests:** unit and integration, see `docs/test-cases/TC-HO-BIL-001.md` Phase J (J-1 to J-9); TC rows J-1 to J-4 of the first build are replaced. Backlog BIL 32 and EMR 43 are closed in `docs/backlog`. |
| Amended (Phase J gate fix) | **2026-10-14 -- Phase J gate fix round (ARCHITECTURE FAIL, CODE_REVIEW FAIL, SECURITY PASS; `docs/gates/HO-BIL-001-J-*.md`); appends to, and where it says so corrects, the Phase J row above.** (1) **Correction of item (4):** the claim that a surgery's appointment "never completes" was false. `SurgeryProcedureCommandHandlers` calls `AppointmentAggregate.CompleteSurgerySlot`, which sets the appointment `Completed` (it raises no `AppointmentCompletedEvent`, so nothing billed it automatically, but the uncaptured list offered it and a manual Appointment capture billed its service fee next to the Surgery source's procedure fee). Fixed: `AppointmentBillableItemSource` returns `NotBillable("AppointmentManagedBySurgery")` when `SurgeryId != null`, the appointment arm of the uncaptured list filters `SurgeryId == null`, and J7 pins both. The surgery source owns the surgery fee. (2) **Owner decisions of 2026-10-14:** (a) dispensing-reversal void -- "Keep it" (owner-ratified; ADR-346 (4)); (b) surgery payer -- "Owner at discharge", the same rule as a visit: `SurgeryAggregate.OwnerAtDischargeId` (`clinic.surgery.owner_at_discharge_id`) is stored by `Discharge` from the cat's owner at that moment and carried on `SurgeryDischargedEvent.OwnerAtDischargeId`; Billing bills that customer and follows the merged-survivor chain; a surgery with none (discharged earlier, or no cat record) is `SurgeryCustomerNotRecorded`; (c) abandoned surgery -- "Drugs only", as built; (d) visits, diagnostic orders and surgeries recorded before these columns existed -- "Bill by hand": no back-fill, manual charges. (3) **Partial capture:** `GetUncapturedSourcesQuery` now lists a visit, dispensing or surgery while it has FEWER recorded charges than billable lines (any charge status counts), so a line skipped for a missing tax class or a refused description stays visible; the event path logs the skipped reason codes (ids only). A surgery with nothing to bill (not `Completed` and no drug draw) is not listed. Backlog row 73's claim that the list "completes the set" is corrected by row 77. (4) **Branch first:** `BillableSourceResult.BranchId` is checked before any non-billable reason, so another branch's source answers `SourceNotFound`. (5) **Migration:** `AddVisitBillingFacts` now adds FIVE nullable columns (`clinic.visit.customer_id`, `clinic.surgery.owner_at_discharge_id`, `clinic.diagnostic_order.{unit_price_amount, unit_price_currency_code, tax_class_id}`; the first Phase J row and the EMR row said three) and replaces `clinic.diagnostic_order_guard()` so the price and tax class are frozen in the database too (additive SQL; the integrator regenerates). (6) **Files outside §13's literal list or in §14 "inputs" (named, as the 2026-10-07 row did):** EMR/PHM/SRG touch points -- `src/Domain/.../Aggregates/Visit/{VisitAggregate,DiagnosticOrderAggregate}.cs`, `Events/Visit/VisitEvents.cs`, `Models/{VisitDtos,DiagnosticDtos,VisitBillingModels}.cs`, `DomainServices/{IVisitBillingReader,VisitBillableLineKindMap}.cs`, `Shared/.../Enums/VisitBillableLineKind.cs`, `Shared/.../Constants/{DiagnosticPricing,SettingKeys.Consultation,SettingDefinitions.Consultation}.cs`, `Application/.../CommandHandlers/Visit/{ConsultationNoteHandlers,DiagnosticHandlers}.cs`, `Infrastructure/.../Consultation/VisitBillingReader.cs`, `Configurations/{VisitConfiguration,DiagnosticOrderConfiguration,SurgeryConfiguration}.cs`, `Api/.../Modules/ConsultationModule.cs`, `Domain/.../Aggregates/Surgery/SurgeryAggregate.cs`, `Events/Surgery/SurgeryEvents.cs`, `Models/SurgeryDtos.cs`, `Application/.../CommandHandlers/Surgery/SurgeryRecoveryCommandHandlers.cs`; tests `tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillingReaderTests.cs` (outside §13's list, kept: it pins the words printed on an invoice and the line-ref limit), `tests/CatEcommerce.Domain.Tests/Aggregates/Billing/PhaseJContractTests.cs`, `tests/CatEcommerce.Application.Tests/CommandHandlers/Billing/PhaseJCaptureTests.cs`, `tests/CatEcommerce.IntegrationTests/Endpoints/BillingPhaseJEndpointTests.cs`. The PHM and SRG consumer notes are ratified by the coordinator (ARCHITECTURE M3). (7) **`.ai/flows/clinic-visit-flow.md` is NOT edited** (§14); the coordinator updates it at merge. The text for it: the event carries `CustomerId` (owner at finalising, stored on `clinic.visit.customer_id`); Billing reads a visit only through `IVisitBillingReader`; diagnostics are priced from `clinical.diagnosticPrice*` settings frozen on the order; the consultation fee is the appointment's; every visit is its own bill. (8) **Recorded, not fixed:** a capture that read a dispensing as `Dispensed` can save just after a reversal voided the (then empty) charge set (backlog 87; the manual correction of row 72 applies). `README.md`'s ADR pointer is bumped to ADR-347; ADR-117 still carries no "narrowed by ADR-344" pointer (append-only; coordinator). CONTRACTS rows 36 to 40. |
| Amended (Phase J re-check) | **2026-10-14 -- combined re-check fixes R1 and R3 (R2, the README pointer bump to ADR-347, is ratified by the coordinator).** R1: the surgery arm of `GetUncapturedSourcesQuery` counts drug lines as `SurgeryBillableItemSource` bills them -- one per ORIGINAL drug entry whose latest correction keeps a SKU and a positive quantity (chains resolved in memory over the candidate surgeries) -- plus the fee for a Completed procedure; tests J7f, J7g, J7h. R3: `AddVisitBillingFacts` adds a NEW trigger `tr_surgery_owner_at_discharge_frozen` (`clinic.surgery_owner_at_discharge_frozen()`) so `clinic.surgery.owner_at_discharge_id` cannot change once set; no existing trigger is touched (test J7i). Backlog rows 95 to 97. |
| Amendments this slice makes to other handovers | Recorded here because this author may not edit other handover files; the coordinator appends the matching §1 Amended rows. **HO-ADM-001**: nine `billing.*` keys appended to `SettingDefinitions` (§10.2) with a new group `Billing`. **HO-NTF-001**: three templates appended to the catalogue (§8.12); `INotificationLinkBuilder` gains `InvoiceLink(invoiceId)` and `ReceiptLink(paymentId)`. **HO-ACC-001**: `ActionRoles` rows appended (§12.3); `UniqueConstraintExceptionFilter` gains Billing mappings (§12.5); Billing subscribes to `CustomerMergedEvent` (§11.6). **README**: ADR ranges ADR-177…194 and ADR-FE-079…086 |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give the business **one set of books for all three lines of business**: every consultation, surgery,
medicine, product, delivery charge and boarding night becomes a **charge**; charges become
**sequentially numbered, itemised, tax-correct invoices**; money received by any v1 method is
**recorded by a named person against a reference** and applied to invoices; corrections are **credit
notes**, never edits; and three **reconciliations** — COD remittances, the bank statement and the
till — prove that every payment actually landed.

**There is no payment gateway in v1** (owner instruction, SRS v1.1, A-2, §10 Phase 5). Money arrives
as cash on delivery, cash in store, card in store through a standalone terminal (recorded, not
processed), or bank transfer confirmed by staff. The SRS says the consequence plainly (§10): *"every
payment passes through a person"*, so the controls that replace a gateway's automatic record —
attributable manual entry (FR-BIL-12, BR-17), second approval above a threshold, reversal instead of
edit, and reconciliation (FR-BIL-7) — are **launch requirements**, and most of this document is about
getting them right.

The slice is also the **billing contract for every area that charges money**. Areas built before it
(Appointments, Consultation, Pharmacy, Surgery) are **pulled from** — Billing captures their billable
items when they finish. Areas built after it (Orders, Boarding, Subscriptions) **push** charges into
Billing inside their own transaction through one port. Both paths land in the same `charge` table,
idempotent by source key (ADR-178).

Six things this slice is careful about:

1. **Numbers.** Invoice and credit-note numbers are sequential **per branch and financial year, with
   no gaps and no reuse**, even under concurrent issue — enforced by a counter row committed in the
   same `SaveChangesAsync` as the document, protected by `xmin`, with a bounded retry (ADR-179). No
   PostgreSQL sequence (sequences leave gaps on rollback).
2. **Immutability.** An issued invoice, a credit note, a payment's amount and reference, a refund and
   an allocation entry are append-only — in the aggregate **and** in the database (triggers, ADR-180,
   D-5, BR-11).
3. **Tax.** Rates come from HO-ADM-001's effective-dated tax classes, are resolved **at issue** and
   frozen on the line; rounding is per line, half away from zero, to the currency's two decimals
   (ADR-181, US-BIL-07 AC2).
4. **Money without floats.** HO-ADM-001's `Money` (`numeric(12,2)` + ISO code, ADR-054) everywhere;
   one pure calculator does all arithmetic (ADR-194).
5. **People, not gateways.** Every payment names the recorder, the method, the reference and the date
   received; above a configurable threshold a **different** staff member must approve it before it
   counts; a mistake is reversed and re-entered, never edited (ADR-183). **No card number or token
   ever enters the system** (C-5), and a reference that looks like a card number is refused.
6. **A seam for Phase 5, not a gateway.** Payment is modelled as *a record of money received*,
   independent of method; behaviour that differs by method is expressed only as capability flags on
   the `PaymentMethod` enumeration. Adding an online method later is a new member, a provider adapter
   and a webhook — no change to invoices, credit notes, refunds or allocations (ADR-182).

§17 builds the slice in **ten phases** (A–J); each compiles, migrates and passes its own tests first.
Phase J (capture from Consultation, Pharmacy and Surgery) waits for those handovers to be merged.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-BIL-01 | M | 5 | Gapless, non-reusable invoice numbers per branch and financial year; concurrency-safe; credit notes on their own series | C |
| US-BIL-02 | M | 5 | Invoices itemise services, products, medicines, boarding nights and fees, with tax per line and each line attributed to its business line | B, C |
| US-BIL-07 | M | 8 | Tax per line from the charge's tax class at the configured rate; rate frozen at issue, so a later rate change never touches issued invoices | C |
| US-BIL-05 | M | 5 | Issued invoices immutable (aggregate + database); corrections by credit note and a linked, re-issued corrected invoice | C, E |
| US-BIL-11 | M | 5 | Staff record a payment with method, amount, currency, reference, date received and their identity; second approval above the threshold; fully audited; reversal and re-entry, never edit | D |
| US-BIL-03 | M | 8 | Partial payments, deposits (earmarked payments), outstanding balance per customer, ageing view | D |
| US-BIL-04 | S | 5 | Several payments of different methods applied to one invoice until the balance clears | D |
| US-BIL-12 | S | 8 | Daily cash-up per branch: cash, in-store card and transfer payments recorded that day with expected totals; counted cash, variance, explanation required before close | F |
| US-BIL-06 | M | 13 | COD remittances reconciled against courier-collected COD payments with shortfalls listed; bank statement import and transfer matching with unmatched entries flagged both ways; till reconciliation through the cash-up | F, G |
| US-BIL-09 | S | 8 | Accounting export of invoices, payments, credit notes, refunds and allocations for any date range; byte-identical on re-run; every row carries a stable document id | H |
| US-BIL-08 | C | 8 | Customer credit (released overpayments, credited amounts not refunded) applied as tender, partially or fully, with the remaining credit shown. **Gift vouchers not built** — OQ-7 | I |
| US-BIL-10 | C | 8 | Loyalty points accrue on paid qualifying spend across clinic, shop and boarding; redeem as a discount shown on the invoice; disabled by default | I |

Also delivered — the **contracts** other areas build against (§8.13, §12.8): the charge port, the
capture pipeline, the source-selector invoice issue, the payment and refund commands, the balance and
group-total queries, and the integration events.

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext` type, `xmin` → `409` | ADR-011, ADR-016 |
| No `CommandService` layer; `SaveChangesAsync` per repository call | ADR-015, ADR-021 |
| **No payment gateway in v1**; payments are COD, cash in store, card in store (recorded), bank transfer (staff-confirmed) | Owner; SRS v1.1 §1.2, A-2, FR-ORD-8, §10 |
| **Identifier + password only**; staff are named users on every action | Owner; SRS v2.2/2.3; HO-ACC-001 |
| **`Money` = `decimal(12,2)` + ISO-4217 code, default `BDT`**; no floating point | HO-ADM-001 ADR-054 |
| **Prices and tax rates are effective-dated; consumers snapshot at transaction time** | HO-ADM-001 ADR-049 |
| Branch-scoping rule: aggregates that exist at a place carry `BranchId`; active branch from `X-Branch-Id` | HO-ADM-001 §8.2, ADR-046 |
| Every endpoint declares access; `[RequireAction]` + the one `ActionRoles` table; customer resources `404` when not the caller's | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| Every aggregate change audited in the same transaction, secrets redacted | HO-ACC-001 ADR-040 |
| Messages only through `INotificationOutbox.Enqueue`, in the command handler before the save; **never from an event handler** | HO-NTF-001 ADR-056, ADR-057 |
| Appointments carry a `ServiceSnapshot` price; no payment at booking | HO-APT-001 ADR-073 |
| `CustomerMergedEvent` is binding on every context holding a customer id | HO-ACC-001 ADR-039 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-BIL-002`](HO-BIL-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-BIL-03 "outstanding balance is visible on the customer profile" | `GET /api/staff/billing/customers/{id}/summary` (and the customer's own `/me/billing/summary`) |
| US-BIL-03 "an ageing view groups them" | `GET /api/staff/billing/receivables/ageing` returns bucketed rows |
| US-BIL-08 "the remaining balance is shown" | Every allocation response and the summary return `freeCredit` after the change |
| US-BIL-10 "the redemption is shown on the invoice" | `loyaltyRedemption` on the invoice view and PDF; per-line `invoiceDiscountShare` |
| US-BIL-12 "listed with expected totals" | `GET /api/staff/billing/cash-ups/{date}` |
| US-BIL-06 "shortfalls are listed", "unmatched entries flagged both ways" | Reconciliation reads (§12.4) |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Where it lands | What this slice delivers |
|---|---|---|
| Recording cash collected on delivery against the order (US-ORD-20), confirming a bank-transfer order (US-ORD-19), invoice at confirmation (US-ORD-08), refunds on return/cancel (US-ORD-12, US-ORD-15), POS tender (US-ORD-16) | **HO-ORD-001** | `IBillableChargeRecorder` (§8.4), `RecordPaymentCommand` with `CashOnDelivery` + collector and `Order` earmark, `IssueInvoiceFromChargesCommand` with a source selector, `IssueCreditNoteCommand`, `IssueRefundCommand`, `PaymentAcceptedEvent` (§12.8) |
| Combined invoice across clinic, shop and boarding (US-ORD-17) | HO-ORD-001 (UX of "add to one invoice") | **Server half built here**: a draft composed from any unbilled charges of one customer, each line attributed to its business line (A4, A5) |
| Boarding deposit calculation, `reserved → confirmed` on deposit, cycle invoicing (US-BRD-20), spending limit (BR-8), dashboard "unpaid balances" (US-BRD-19), early check-out credit (US-BRD-16) | **HO-BRD-001** | Charges with a `BillingGroup` of the stay; earmarked deposit payments; `PaymentAcceptedEvent`; `IssueInvoiceFromChargesCommand` with `BillingGroup` + `ServiceDateTo`; `GetBillingGroupSummaryQuery` |
| Visit treatments/consumables/diagnostics, dispensed medicines, surgery items | **HO-EMR-001**, **HO-PHM-001**, **HO-SRG-001** | The capture pipeline and the **assumed** source contract (§8.13); Phase J builds the three item sources once those handovers are merged |
| Portal dashboard "outstanding balances" (US-POR-01) | HO-POR-001 | `GET /api/customers/me/billing/summary` |
| Revenue by line of business, receivables, refunds, tax summary, payment reconciliation and cash-up reports (§8.2, US-RPT-03) | HO-RPT-001 | The tables; `BusinessLine` on every line; reconciliation and cash-up reads |
| Thermal receipt printing over ESC/POS (HW-1) | HO-ORD-001 (POS) | Receipt data endpoint (`GET …/payments/{id}/receipt`) |
| Personal-data export including invoices (US-ACC-13) | HO-ACC amendment (not written) | Nothing — flagged in OQ-21 |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **Payment gateway — SRS Phase 5**, backlog §18.5: online card/wallet payment, saved tokens, automated refund to card/wallet, gateway settlement reconciliation, subscription auto-charge and retry, payment webhooks | **Forbidden scope** (§14). The seam is designed (ADR-182) so none of this needs rework later |
| Gift vouchers (FR-BIL-9, US-BIL-08 title) | Not built — the AC evidences store credit only. OQ-7; a later HO-BIL-001 Amended row delivers it once the owner defines how vouchers are sold |
| Scheduled accounting export (§3.3 "scheduled") | On-demand only; OQ-19 |
| Target accounting system and format (SRS Q-13) | A documented canonical CSV bundle (§8.10); OQ-9 |
| In-store card terminal settlement report (SRS Q-3c) | Card payments are listed in the cash-up; no terminal-batch reconciliation. OQ-13 |
| Courier master data (SRS Q-4) | COD collections carry a courier **code and name snapshot**; HO-ORD-001 owns the courier list. OQ-11 |
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Invoice, receipt and refund messages are enqueued; outside Development only the in-app copy is delivered |
| Bank statement format (varies by bank) | A canonical CSV (§8.9); OQ-10 |
| Virus scanning (NFR-SEC-5) | The uploaded statement is **parsed in memory and never stored as a file**, so there is nothing to scan or serve; size and content checks still apply |
| QuestPDF licence (HO-CAT-001 §20.6) | Invoice and credit-note PDFs reuse the existing package; §20.9 |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, a second host, a `CommandService` layer, `202 Accepted`,
`[Authorize]`. **No payment gateway, provider SDK, webhook endpoint, online `PaymentMethod` member,
payment-token column, or card-number field** (C-5, §18.5). **No `switch`/`if` on a `PaymentMethod`
identity outside `PaymentMethodRules`** — behaviour comes from capability flags (ADR-182). **No update
or delete** of an issued invoice, credit note, refund, allocation entry, or a payment's amount,
method, reference or date (ADR-180). **No hard delete** of any Billing row (D-6). **No `float`/`double`**
for money or tax. **No PostgreSQL sequence for invoice or credit-note numbers**, no advisory lock, no
`SELECT … FOR UPDATE`, no explicit `BeginTransaction` (ADR-179). **No enqueue from an event handler.**
**No background worker** — nothing in Billing is timed. **No second `DbContext` type.** No new NuGet
package.

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 merged** | ❌ **not implemented at baseline** — §20.1 |
| **HO-EMR-001, HO-PHM-001, HO-SRG-001 merged** | ❌ **handovers not written at baseline.** Needed **for Phase J only**; Phases A–I do not depend on them — §20.2 |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| QuestPDF 2026.9.0 already referenced by Infrastructure (F32) | ✅ present; ⚠️ licence unconfirmed — §20.9 |
| New NuGet packages | **None.** `System.IO.Compression`, `System.Security.Cryptography`, CSV parsing by hand (§8.9) |

The shared-database rules of HO-CAT-001 §16.1, HO-ACC-001 §16.1 and HO-APT-001 §16.1 apply. **Every
test creates its own branch** (so invoice-number series, cash-ups and reconciliation are per-test) and
its own customers. Invoice numbers are **not** asserted as literal values — only as contiguous within
the test's own series (§16.1).

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, the markdown wins.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | §4 the command path (handler loads, aggregate enforces, handler saves) and **"when a Domain Service is warranted"** — `InvoiceCalculator`, `BankMatchSuggester`, `CashUpCalculator`, `AgeingCalculator` are pure domain services because they span many aggregates; §6.2 stamp `ValidateCommand = true` (F3) |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | Commands and queries only through `IInMemoryDispatcher`; queries never write — the accounting export's run record is a separate command (§11.4 C26) |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Names the **Invoice** context (`InvoiceAggregate`, `PaymentAggregate`, credit notes) — this slice names it `Billing` and widens it (ADR-177, OQ-1). **Rule 1** "one aggregate mutated per command" — the posting operations are a recorded exception (ADR-184); **rule 3** unbounded collections stay out of aggregates — why charges, loyalty entries and bank matches are their own roots; **rule 4** cross-aggregate logic in domain services |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Every namespace in §11/§12. It lists `Invoice` and `Payment` as feature segments; this slice uses the single segment **`Billing`** (ADR-177). Do not invent others |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 are binding**; ADR-014 (idempotency at the HTTP edge — ADR-187 here), ADR-016 (`xmin` — the basis of numbering, ADR-179), ADR-020 (event dispatch is ours, on the runtime type), ADR-021 (per-call save — the basis of ADR-184's follow-up commit) |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; §4 uniqueness pre-check plus index; **§6 rule placement** — totals, status and caps live in the aggregates; existence and cross-record lookups in the handler |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking()`, project before materialising; balances, ageing, reconciliation and export are queries |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 the repository (already implemented — **F31** is what ADR-184 relies on); §4 owned collections; §6 `xmin`; §7 indexes (invoice number is named there as a unique natural key); §8 column types — **note** it says money is `numeric(19,4)`; this slice follows HO-ADM-001 ADR-054 (`numeric(12,2)`) — F33, OQ-17 |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §3 a failing event handler fails a request that already committed — why capture handlers run in a **fresh scope and swallow** (ADR-178); §4 anything leaving the process goes through the outbox (invoice/receipt/refund messages; the accounting export is a **download**, not an outbound call, so it is not outboxed) |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | Lists `PaymentMethod` (CashOnDelivery, CashInStore, CardInStore, BankTransfer) and `InvoiceStatus` (Draft, Issued, PartPaid, Paid, Credited) — used as listed, plus `Discarded` (ADR-180). Its note forbids an online member before the gateway exists — honoured |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | Validators as `IValidator<T>`; `ValidateScopes`/`ValidateOnBuild` on; `IServiceScopeFactory` for the capture scope |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; lifetimes; the never-register list |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`HO-ACC-001`](HO-ACC-001.md) §7, §11.9, §12.4, §12.8, §16.1 | F11–F18; audit capture; response rules R1–R8; `RequireActionFilter`; shared-database rules |
| [`HO-ADM-001`](HO-ADM-001.md) §8.6, §8.7, §12.4 | Prices and tax rates "at *t*"; the snapshot contract; settings; money JSON shape (`amount` as a string) |
| [`HO-NTF-001`](HO-NTF-001.md) §8.2, §8.4, §8.8 | Template definition rules; the producer rules; the link builder |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001
F28–F30 apply. Additionally, read from the code at `4007225` unless marked:

| # | Fact | Consequence |
|---|---|---|
| F31 | `EfAggregateRootRepository.CommitAsync` calls `db.SaveChangesAsync()` with **no explicit transaction**, then dispatches **only the passed aggregate's** `Events` and clears them. `UpdateAsync` does not call `.Update()` on a tracked aggregate | (a) Several roots mutated in one context commit atomically in the first repository call (with F23). (b) **Events raised on the other roots are not dispatched by that call** — ADR-184 requires a follow-up `UpdateAsync` on each secondary root, which finds no pending changes, writes nothing, and dispatches its events. (c) A root added by a **port** (the charge recorder) never has its events dispatched — charges raise no events |
| F32 | `QuestPDF` 2026.9.0 is referenced by `CatEcommerce.Infrastructure` and used by `QuestPdfCatMedicalHistoryRenderer`; its licence is unconfirmed (HO-CAT-001 §20.6) | Invoice and credit-note PDFs use it; no new package; §20.9 inherits the stop |
| F33 | HO-ADM-001 maps `Money` as `amount numeric(12,2)` + `currency_code char(3)` and rejects negative amounts (its U-D7); `.ai/persistence-ef-core.md` §8 says `numeric(19,4)`; SRS C-6 says "integer minor units" | Billing uses HO-ADM-001's `Money` unchanged (ADR-194). Every derived amount is rounded explicitly by `InvoiceCalculator`; directions (credit, refund, release) are expressed by the record kind, never by a negative `Money` |
| F34 | `AppointmentAggregate.ServiceSnapshot` holds `Name`, `Category`, `DurationMinutes`, `Price` — **no tax class**; `AppointmentCompletedEvent` carries ids and instants only (HO-APT-001 §8.2, §11.5) | The appointment item source reads the appointment row and the service's **current** `TaxClassId` from `admin.clinic_service` at capture time (§8.13) |
| F35 | *(EF Core behaviour, not yet exercised in this repo — pinned by I-C3)* `DbUpdateConcurrencyException.Entries` lists the entries whose `xmin` check failed; `EntityEntry.ReloadAsync()` refreshes one entry while other tracked changes stay pending | The numbering retry (ADR-179) reloads only the series row and re-saves |
| F36 | *(framework behaviour — pinned by U-H2)* `ZipArchiveEntry.LastWriteTime` is settable and entries are written in insertion order | A fixed timestamp and fixed entry order make the export zip byte-identical on re-run (ADR-190) |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Billing context — schema `billing`
BillingProfileAggregate       (root, Id == BranchId)                          billing.billing_profile
└── BankAccounts [owned many ≤ 5]                                             billing.billing_profile_bank_account
DocumentNumberSeriesAggregate (root) → BranchId, FinancialYear, DocumentKind  billing.document_number_series
ChargeAggregate               (root) → CustomerId, BranchId, source key      billing.charge
InvoiceAggregate              (root) → CustomerId, BranchId                   billing.invoice
└── Lines [owned many ≤ 500]  InvoiceLine                                     billing.invoice_line
PaymentAggregate              (root) → CustomerId, BranchId                   billing.payment
├── Entries [owned many ≤ 200] AllocationEntry (Allocate | Release)           billing.payment_allocation_entry
└── RequestedAllocations [owned many ≤ 20]  (pending approval only)           billing.payment_requested_allocation
CreditNoteAggregate           (root) → InvoiceId                              billing.credit_note
└── Lines [owned many ≤ 500]  CreditNoteLine                                  billing.credit_note_line
RefundAggregate               (root) → PaymentId, CreditNoteId?               billing.refund
CashUpAggregate               (root) → BranchId + BusinessDate (unique)       billing.cash_up
CodRemittanceAggregate        (root) → CourierCode                            billing.cod_remittance
└── Lines [owned many ≤ 1000] RemittanceLine                                  billing.cod_remittance_line
BankStatementAggregate        (root)                                          billing.bank_statement
└── Lines [owned many ≤ 5000] BankStatementLine                               billing.bank_statement_line
BankMatchAggregate            (root) → StatementLineId, PaymentId             billing.bank_match
AccountingExportRunAggregate  (root, append-only)                             billing.accounting_export_run
LoyaltyAccountAggregate       (root, Id == CustomerId)                        billing.loyalty_account
LoyaltyEntryAggregate         (root, append-only) → CustomerId                billing.loyalty_entry
```

**Why charges are their own root.** A customer accumulates charges without bound, from several areas,
before anyone decides which invoice they go on (rule 3). A charge is immutable once recorded except
for `Void`. **Whether it is billed is derived**: a charge is billed when a line of a non-discarded
invoice references it, and a filtered unique index on `invoice_line.charge_id` makes "billed at most
once" true under concurrency without mutating the charge (ADR-178).

**Why payments own their allocation entries.** A payment is the money; allocation says where it went.
Entries are append-only (`Allocate` / `Release`, each with its own id and instant), so history is
never rewritten and the accounting export can list them by date (ADR-185). The invoice keeps only the
**cached totals** it needs for its own invariants (`PaidTotal`, `CreditedTotal`).

**One aggregate mutated per command — with one recorded family of exceptions (ADR-184), "posting":**
issue (document + series), allocation / approval / reversal (payment + the invoices it touches), credit
note (credit note + series + invoice + payments whose allocation is released), refund (refund +
payment), loyalty redemption and accrual (account + entry + draft invoice where relevant). Each commits
in **one** `SaveChangesAsync`; every root has `xmin`; secondary roots' events are dispatched by the
follow-up call F31(b) describes, implemented once in `BillingPosting.CommitAsync(primaryRepo, primary,
params secondaries)` (Application, static helper over repositories — not a service layer).

### 8.2 Business line, item kind, source

Every charge and invoice line carries three classifications:

| Field | Values | Purpose |
|---|---|---|
| `BusinessLine` | `Clinic`, `Shop`, `Boarding` | Revenue by line of business (§8.2 SRS, US-RPT-03, US-ORD-17 "attributed to its business line") |
| `ItemKind` | `Service`, `Product`, `Medicine`, `BoardingNight`, `Fee` | FR-BIL-2 "services, products, medicines, boarding nights and charges" |
| `SourceKind` + `SourceId` + `SourceLineRef` | `Appointment`, `Visit`, `Dispense`, `Surgery`, `Order`, `BoardingStay`, `Manual` | Where the charge came from; the idempotency key |

Default mapping (enforced by `ChargeRules.DefaultBusinessLine`, overridable only by `Manual`):
Appointment/Visit/Dispense/Surgery → `Clinic`; Order → `Shop`; BoardingStay → `Boarding`.

### 8.3 `ChargeAggregate`

| Field | Type | Notes |
|---|---|---|
| `CustomerId`, `BranchId`, `CatId?` | Guid | The customer billed; the branch where the service/sale happened |
| `BusinessLine`, `ItemKind`, `SourceKind`, `SourceId`, `SourceLineRef` | | `SourceLineRef` ≤ 100, default `"main"`; unique triple `ix_charge_source` |
| `BillingGroupKind?`, `BillingGroupId?` | `BillingGroupKind` (`BoardingStay`) / Guid | "Put this on the stay's invoice" — FR-BRD-15, FR-SRG-9. A vet charge during a stay has `SourceKind = Visit` and group = the stay |
| `Description` | string ≤ 300 | Plain text as it will print |
| `Quantity` | `decimal(10,3)` > 0 | Boarding nights and products are whole; medicines may be fractional |
| `UnitPrice` | `Money` | Snapshot from the source — **never re-read** (HO-ADM-001 ADR-049) |
| `LineDiscount` | `Money` | ≥ 0, ≤ `Quantity × UnitPrice` (rounded); order-level discounts are apportioned by HO-ORD-001 before recording |
| `TaxClassId` | Guid | Must exist in `admin.tax_class`; **rate is not stored here** (resolved at issue, ADR-181) |
| `ServiceDate` | `DateOnly` | Branch-local date of supply; selects charges for cycle invoices (US-BRD-20) |
| `ReportingTag?` | string ≤ 50 | e.g. a campaign code (US-SRG-11 reporting) |
| `Status` | `ChargeStatus` | `Active`, `Voided` |
| `RecordedOn`, `RecordedBy`, `VoidedOn?`, `VoidedBy?`, `VoidReason?` (≤ 500) | | |

Methods: `Record(ChargeRequestDto)` (validates the fields above, currency equals
`commerce.currencyCode` → else `CurrencyMismatch`), `Void(reason, now)` (only `Active`; **the handler
refuses when a non-discarded invoice line references it** → `ChargeAlreadyInvoiced`). **No domain
events** (F31(c)); voids are audited by the change-capture interceptor and a `BusinessEvent` row
(§11.6).

### 8.4 Push path — `IBillableChargeRecorder` (the port for areas built after Billing) — ADR-178

`Domain.DomainServices`, implemented by `Infrastructure/Billing/EfBillableChargeRecorder`:

```csharp
public interface IBillableChargeRecorder
{
    /// Adds a ChargeAggregate to the caller's unit of work WITHOUT saving (same shape as
    /// INotificationOutbox, F23). Returns the charge id — the existing one if this source key was
    /// already recorded (pre-check against tracked entries and the table).
    Task<Guid> RecordAsync(ChargeRequestDto request, CancellationToken ct = default);

    /// Voids the unbilled charge with this source key in the caller's unit of work.
    /// Absent → no-op. Already invoiced → DomainRuleViolationException(ChargeAlreadyInvoiced);
    /// the producer then corrects by credit note through Billing.
    Task VoidAsync(ChargeSourceKey key, string reason, CancellationToken ct = default);
}
```

**Producer rules** (checked by the ARCHITECTURE gate on HO-ORD-001 / HO-BRD-001 / HO-SUB-001):

1. Call it **in the command handler, before** the producer's `SaveAsync`/`UpdateAsync`, so the charge
   commits atomically with the business change (an order line, a boarding night, an extension).
2. **Never from a domain event handler** (F24 — the change is already durable).
3. Supply prices and discounts already computed; Billing validates, never reprices.
4. The race where two requests record the same source key between pre-check and save surfaces as
   `23505 ix_charge_source` → `409 ChargeAlreadyRecorded` on the producer's request (§12.5). The
   producer's client retries; the retry sees the existing charge.

### 8.5 Pull path — capture from areas built before Billing — ADR-178

Appointments exist; Consultation, Pharmacy and Surgery are built before Billing in the delivery order
and cannot call a port that does not exist yet. Billing therefore **captures** their billable items:

```
Source area commits, raises event ──► Billing event handler (thin)
      │   AppointmentCompletedEvent          creates a NEW DI scope (IServiceScopeFactory)
      │   VisitFinalisedEvent*               dispatches CaptureSourceChargesCommand(kind, id) there
      │   PrescriptionDispensedEvent*        catches every exception → logs Error (ids only), returns
      │   SurgeryCompletedEvent*
      ▼
CaptureSourceChargesCommandHandler ──► IBillableItemSource[kind].GetItemsAsync(id)
                                        ──► one ChargeAggregate per item not yet recorded (source key)
                                        ──► one SaveAsync per new charge
      * assumed contracts — §8.13
```

- **Fresh scope**: the capture's `DbContext` is separate from the source's, so a Billing failure can
  never poison the source's context or fail the source's request (`.ai/domain-events-and-outbox.md` §3).
- **Idempotent**: re-running a capture records only the missing items (`ix_charge_source`).
- **Recoverable**: `GetUncapturedSourcesQuery` lists completed appointments (and, from Phase J,
  finalised visits, dispenses, completed surgeries) **with no charge**, and staff re-run
  `POST …/sources/{kind}/{id}/capture` (C4). A capture that failed is therefore visible, never lost.
- A source item with price 0 is captured (it prints as a zero line); an item whose tax class is missing
  is **skipped and reported** in the capture result (`TaxClassNotFound`), never guessed.

**Appointment item source (Phase B, buildable now).** For a `Completed` appointment: one charge,
`ItemKind = Service`, `BusinessLine = Clinic`, `Description = ServiceSnapshot.Name`, `Quantity = 1`,
`UnitPrice = ServiceSnapshot.Price`, `TaxClassId` = the service's current tax class (F34),
`ServiceDate` = the local date of `CompletedOn` in the branch zone, `CatId`, `CustomerId`,
`BranchId`, `SourceLineRef = "service"`. Walk-ins are appointments and are charged the same way.
**The appointment fee is charged here and only here** — HO-EMR-001 charges *additional* items (§8.13,
OQ-14).

### 8.6 `InvoiceAggregate` — US-BIL-01, 02, 05, 07

| Field | Notes |
|---|---|
| `BranchId`, `CustomerId`, `Status` (`InvoiceStatus`), `CurrencyCode` | |
| `Number?`, `FinancialYear?` (e.g. `"2026-27"`), `SequenceNumber?` | Assigned at issue (§8.7) |
| `IssuedOn?` (instant), `IssueDate?` (branch-local `DateOnly`), `DueOn?` | `DueOn = IssueDate + billing.paymentTermsDays` (0 → due on issue, OQ-6) |
| `SellerSnapshot?` (owned) | From `BillingProfileAggregate` at issue: legal name, tax registration number, address, phone, email, footer, bank accounts — NFR-LEG-3 |
| `BillToSnapshot?` (owned) | Customer full name, mobile, email, default address text at issue — retained under D-9 (ADR-192) |
| `Lines` (owned ≤ 500) | §below |
| `NetTotal`, `DiscountTotal`, `TaxTotal`, `Total` | Computed by `InvoiceCalculator` at issue and frozen; on drafts, an **estimate** at today's rates |
| `PaidTotal`, `CreditedTotal` | Cached; changed only by posting (§8.8, §8.9) |
| `LoyaltyRedemption?` (owned) | `Points`, `Value` (`Money`), `LoyaltyEntryId` — draft only until issue (§8.11) |
| `CorrectsInvoiceId?`, `CorrectsCreditNoteId?` | A corrected re-issue (US-BIL-05 AC2) |
| `Notes?` ≤ 500, `CreatedBy`, `IssuedBy?`, `IssueIdempotencyKey?` | |

**`InvoiceLine`** — `Id`, `ChargeId?` (null only on corrected re-issues), `CorrectsInvoiceLineId?`,
`BusinessLine`, `ItemKind`, `SourceKind`, `SourceId`, `CatId?`, `Description`, `Quantity`, `UnitPrice`,
`LineDiscount`, `InvoiceDiscountShare` (the apportioned loyalty discount), `TaxClassId`,
`TaxClassCode` (snapshot), **`TaxRatePercent`** (frozen at issue), `NetAmount`, `TaxAmount`,
`GrossAmount`, `SortOrder`.

**Status** (Appendix C plus `Discarded`, ADR-180):

```
Draft ──Issue──► Issued ──► PartPaid ──► Paid
  │                 └───────────┴─────────┴──► Credited (CreditedTotal == Total)
  └──Discard──► Discarded
```

After issue the status is **derived** from the totals on every posting, by one function
`InvoiceStatusRules.Derive(total, paid, credited)`:
`outstanding = Total − CreditedTotal − PaidTotal` (invariant: never negative);
`credited == total` → `Credited`; `outstanding == 0 && paid > 0` → `Paid`; `paid > 0` → `PartPaid`;
else `Issued`.

| Method | From | Rules | Event |
|---|---|---|---|
| `CreateDraft(dto)` | — | ≥ 1 line; every charge `Active`, same customer, same branch, same currency, not billed (handler pre-check + index) | `DraftInvoiceCreatedEvent` |
| `SetDraftLines(lines)` | `Draft` | Replaces lines; clears a loyalty redemption whose value now exceeds the qualifying net (`LoyaltyRedemptionExceedsNet`) | `DraftInvoiceChangedEvent` |
| `Discard(now)` | `Draft` | Lines removed from the index's scope (filtered `WHERE NOT discarded`), so charges return to unbilled | `DraftInvoiceDiscardedEvent` |
| `Issue(number, taxRates, seller, billTo, now, zone, termsDays)` | `Draft` | Every line's tax class has a rate at `now` (else `TaxRateNotConfigured` naming the class); seller profile present (else `BillingProfileMissing`); totals computed; lines frozen | `InvoiceIssuedEvent` |
| `ApplyPayment(amount)` / `ReleasePayment(amount)` | issued states | `amount ≤ outstanding` (else `AllocationExceedsOutstanding`); re-derives status | `InvoiceStatusChangedEvent` (only when the status changes) |
| `ApplyCredit(amount)` | issued states | `CreditedTotal + amount ≤ Total` (else `CreditExceedsInvoice`); the handler releases any resulting overpayment first (§8.9) | same |

**Immutability — ADR-180.** The aggregate exposes no method that changes a line or a content field of
a non-draft invoice. The migration adds two triggers (hand-written SQL, read before commit):

- `trg_invoice_immutable` — `BEFORE UPDATE OR DELETE ON billing.invoice`: `DELETE` always raises;
  `UPDATE` raises when `OLD.status <> Draft` **and** any content column `IS DISTINCT FROM` its old value
  (every column except `paid_total`, `credited_total`, `status`, `xmin`-driven version fields and
  `last_updated_*`).
- `trg_invoice_line_immutable` — `BEFORE INSERT OR UPDATE OR DELETE ON billing.invoice_line`: raises
  when the parent invoice's status is not `Draft`/`Discarded` for insert/update, and on delete unless
  the parent is `Draft`.

Status values in trigger SQL are written from `InvoiceStatus.X.Value` in the migration code, never
typed as literals (U-X3). Credit notes, refunds, allocation entries and bank matches get equivalent
append-only triggers (`DELETE` always raises; `UPDATE` raises on content columns).

### 8.7 Numbering — US-BIL-01 — ADR-179

**`DocumentNumberSeriesAggregate`** — `BranchId`, `FinancialYear` (string, `"2026-27"`),
`DocumentKind` (`Invoice`, `CreditNote`), `NextSequence` (long, starts 1). Unique
`ix_document_number_series (branch_id, financial_year, document_kind)`.

- **Financial year**: from the branch-local issue date and `billing.financialYearStartMonth`
  (default **7**, July — the Bangladesh fiscal year; OQ-2). A date in July 2026–June 2027 → `"2026-27"`;
  with start month 1 → `"2026"`. `FinancialYearRules.For(date, startMonth)`, pure, tested.
- **Format**: `{prefix}-{branchCode}-{fyCompact}-{sequence:D6}` → `INV-MAIN-2627-000123`,
  `CN-MAIN-2627-000007`. `DocumentNumberFormat.Format(...)`, pure. The number is unique
  (`ix_invoice_number`, `ix_credit_note_number`) as a backstop.
- **Allocation** (`IDocumentNumbering` port, `Infrastructure/Billing/DocumentNumbering`):
  1. `EnsureSeriesAsync(branch, fy, kind)` executes `INSERT … ON CONFLICT DO NOTHING` (a row with
     `next_sequence = 1` creates no gap), then loads the row **tracked**.
  2. `series.Take()` returns `NextSequence` and increments it. The document is issued with that
     number. Both are tracked; **one** `SaveChangesAsync` commits both (F23).
  3. Two concurrent issuers: the second `UPDATE … WHERE xmin = …` affects 0 rows →
     `DbUpdateConcurrencyException`. `IDocumentNumbering.SaveWithRetryAsync(Func<Task> save,
     Action<long> renumber)` catches it **only when every failing entry is a series row** (F35),
     reloads the series, calls `renumber` with a fresh `Take()`, and saves again — up to
     `BillingRules.MaxNumberingAttempts` (5). Any other concurrency failure rethrows (`409`).
  4. The number is taken and committed with the document or not at all — **no gap, no reuse**.
     A failed issue rolls back the increment with the invoice.

### 8.8 `PaymentAggregate` — US-BIL-11, 03, 04 — ADR-182, ADR-183, ADR-185

| Field | Notes |
|---|---|
| `BranchId`, `CustomerId` | |
| `Method` | `PaymentMethod` (§8.8.1) |
| `Amount` | `Money` > 0, currency = `commerce.currencyCode` |
| `ReceivedOn` | `DateOnly` — the date the money was received (may be earlier than recording; not in the future in branch time; not more than `BillingRules.MaxBackdatedReceiptDays` (31) ago) |
| `ExternalReference?` | ≤ 100; required when the method's `RequiresReference`; **refused if it looks like a card number** (§8.8.2) |
| `Collector?` (owned) | `Kind` (`Staff` \| `Courier`), `StaffAccountId?`, `CourierCode?` (≤ 30), `CourierName?` (≤ 100) — required when `RequiresCollector` |
| `Earmark?` (owned) | `Kind` (`EarmarkKind`: `Order`, `BoardingStay`), `SourceId`, `Label` (≤ 200, e.g. *"Deposit — boarding 12–19 Oct"*), `ReleasedOn?`, `ReleasedBy?`, `ReleaseReason?` |
| `Status` | `PendingApproval`, `Accepted`, `Rejected`, `Reversed` |
| `RecordedBy`, `RecordedOn`, `Notes?` (≤ 500) | BR-17 |
| `ApprovedBy?`, `ApprovedOn?`, `RejectedBy?`, `RejectedOn?`, `RejectionReason?` | |
| `AcceptedOn?`, `BusinessDate?` | `BusinessDate` = branch-local date of acceptance (§8.10) |
| `ReceiptNumber?` | `bigint` from the PostgreSQL sequence `billing.receipt_number_seq` — **gaps allowed** (a receipt number is not a legal document number); printed `R-000123`. Assigned by the database on insert |
| `ReversedBy?`, `ReversedOn?`, `ReversalReason?`, `ReversalBusinessDate?` | |
| `Entries` (owned ≤ 200) | `AllocationEntry(Id, Kind Allocate\|Release, InvoiceId, Amount, OccurredOn, By, ReleaseReason?)` — append-only |
| `RequestedAllocations` (owned ≤ 20) | `(InvoiceId, Amount)` captured at recording, applied on approval, then cleared |
| `RefundedTotal` | Cached; increased only by refunds |
| `IdempotencyKey?` | ≤ 100; unique with `RecordedBy` (ADR-187) |

**Derived**: `AllocatedTo(invoice) = Σ Allocate − Σ Release` for that invoice;
`Unallocated = Amount − Σ(effective allocations) − RefundedTotal` — **invariant ≥ 0**.
A payment's unallocated amount **is customer credit** (ADR-185): a deposit (earmarked), an overpayment,
or money released by a credit note. There is no separate store-credit ledger.

| Method | From | Rules | Event |
|---|---|---|---|
| `Record(dto, threshold)` | — | Method flags (§8.8.1); `Amount > threshold` → `PendingApproval` with requested allocations; else `Accepted` immediately with allocations applied in the same save | `PaymentRecordedEvent`; when accepted also **`PaymentAcceptedEvent`** |
| `Approve(approver, now)` | `PendingApproval` | **`approver ≠ RecordedBy`** → else `ApproverMustDiffer`; requested allocations applied, each capped at the invoice's outstanding at approval time; the remainder stays unallocated (reported) | `PaymentApprovedEvent`, `PaymentAcceptedEvent` |
| `Reject(approver, reason, now)` | `PendingApproval` | Reason required; approver ≠ recorder | `PaymentRejectedEvent` |
| `Allocate(invoiceId, amount, by, now)` | `Accepted` | `amount ≤ Unallocated`; an earmarked payment allocates only to an invoice containing a line whose `(SourceKind, SourceId)` or billing group matches the earmark, unless the earmark was released (`EarmarkMismatch`) | `PaymentAllocatedEvent` |
| `Release(invoiceId, amount, reason, now)` | `Accepted` | `amount ≤ AllocatedTo(invoice)`; reasons `CreditNoteExcess`, `PaymentReversed` only — **staff cannot "move" a payment**; a mis-allocation is corrected by reversal and re-entry (US-BIL-11 AC3) | `PaymentAllocationReleasedEvent` |
| `ReleaseEarmark(by, reason, now)` | `Accepted`, earmarked | Reason required | `PaymentEarmarkReleasedEvent` |
| `RecordRefund(refundId, amount)` | `Accepted` | `amount ≤ Unallocated` | — (the refund raises the event) |
| `Reverse(by, reason, now)` | `Accepted` | Reason required; `RefundedTotal == 0` (else `PaymentHasRefunds` — reverse the refund's effect by recording a new payment); releases every effective allocation (the handler applies `ReleasePayment` on each invoice in the same save) | `PaymentReversedEvent` |

Amount, method, reference, received date, collector and recorder have **no setter after `Record`**
(US-BIL-11 AC3; `trg_payment_immutable` refuses changes to those columns).

#### 8.8.1 `PaymentMethod` and the gateway seam — ADR-182

`PaymentMethod : Enumeration` (Shared) — the four v1 members listed in `.ai/enumeration-pattern.md`,
**each carrying its behaviour as data**. `PaymentMethodRules` (Shared) is the **only** place that reads
a method's identity; everything else reads the flags.

| Member (Value) | `CountsInTill` | `RequiresReference` (label) | `RequiresCollector` | `IsOnline` | `SupportsAutomatedRefund` | `AllowedRefundMethods` (OQ-16) |
|---|---|---|---|---|---|---|
| `CashInStore` (1) | ✅ | ❌ (*"Receipt number"*, optional) | ❌ | ❌ | ❌ | `CashInStore` |
| `CardInStore` (2) | ❌ (listed separately) | ✅ *"Terminal slip number"* | ❌ | ❌ | ❌ | `CashInStore` |
| `BankTransfer` (3) | ❌ (listed separately) | ✅ *"Transfer reference"* | ❌ | ❌ | ❌ | `BankTransfer` |
| `CashOnDelivery` (4) | ✅ **only when `Collector.Kind == Staff`** | ❌ | ✅ | ❌ | ❌ | `CashInStore`, `BankTransfer` |

`GET /api/billing/payment-methods` returns these flags so the UI renders fields from data, not from a
hardcoded list (HO-BIL-002 ADR-FE-082). **U-D1 fails the build** if a new member lacks any flag, and
**U-X4** scans the Application, Read and Api assemblies' IL/source for `PaymentMethod.<Member>`
references outside `PaymentMethodRules` and the enumeration itself.

**Phase 5 — what will be added, and why nothing here changes** (recorded in ADR-182, not built):
an online member with `IsOnline = true`, `SupportsAutomatedRefund = true`; a `PaymentIntentAggregate`
for a checkout awaiting a provider; a provider adapter behind a port; a signed, idempotent webhook
(COM-3) that ends in the **same** `Record` + `Accept` path with `RecordedBy` = a system identity and
`ExternalReference` = the provider transaction id; automated refund chosen when
`SupportsAutomatedRefund`. Invoices, allocations, credit notes, refunds, cash-up and export read only
flags and records of money received, so they are untouched.

#### 8.8.2 C-5 — no card number, ever

`CardNumberGuard.LooksLikeCardNumber(reference)` (Shared, pure): strip spaces and dashes; a run of
**13–19 digits passing Luhn** → `true`. Applied to `ExternalReference`, `Notes`, remittance and refund
references and bank-statement descriptions **on import** (a matching statement description is stored
with the digit run masked to its last four). A payment or refund whose reference or notes match is
refused with `ReferenceLooksLikeCardNumber` (*"This looks like a card number. Enter the terminal slip
number instead — card numbers must never be recorded."*). The refused value is **never logged**.

### 8.9 Credit notes and refunds — US-BIL-05 AC2, FR-BIL-5, FR-ORD-17 — ADR-185, ADR-186

**`CreditNoteAggregate`** — `InvoiceId`, `BranchId`, `CustomerId`, `Number` (CN series, §8.7),
`Kind` (`Correction`, `Return`, `Cancellation`, `Goodwill`), `Reason` (1…1000), `Lines` (owned:
`InvoiceLineId`, `Quantity` credited or `Amount` credited, `NetAmount`, `TaxRatePercent` = **the
invoice line's frozen rate**, `TaxAmount`, `GrossAmount`), `Total`, `ReleasedToCustomerCredit`
(`Money`), `IssuedOn`, `IssueDate`, `IssuedBy`, `IdempotencyKey?`.

`IssueCreditNoteCommand` — one posting (ADR-184):
1. Load the invoice (issued state); load existing credit notes for it; per line, credited so far +
   this credit ≤ the line's gross (`CreditExceedsLine`). "Full credit" = every remaining amount.
2. `InvoiceCalculator.Credit(...)` computes lines at the original rates, same rounding (ADR-181).
3. If `PaidTotal > Total − (CreditedTotal + credit)` the excess is **released** from the invoice's
   payments, **newest allocation first** (`Release(…, CreditNoteExcess)`); the released amount becomes
   those payments' unallocated credit and is recorded on the credit note as
   `ReleasedToCustomerCredit`. **This is "refund as store credit"** (FR-ORD-17): the customer keeps it
   as credit unless a cash/transfer refund is then issued from it.
4. Number from the CN series; `invoice.ApplyCredit(total)`; save once with numbering retry.

**Corrected re-issue** (US-BIL-05 AC2): `CreateCorrectedDraftCommand(NewInvoiceId, CreditNoteId)`
creates a draft whose lines copy the credited invoice's lines (`ChargeId = null`,
`CorrectsInvoiceLineId` set), with `CorrectsInvoiceId`/`CorrectsCreditNoteId`; staff edit those lines
(`SetCorrectedDraftLinesCommand` — description, quantity, unit price, discount, tax class; no charge
lines) and issue it normally. The three documents link both ways in their views.

**`RefundAggregate`** — money **out**: `PaymentId`, `CreditNoteId?`, `BranchId`, `CustomerId`,
`Method` (`RefundMethod`: `CashInStore`, `BankTransfer`), `Amount`, `Reference?` (≤ 100; required for
`BankTransfer`; card-number guard), `Reason` (1…1000), `IssuedBy`, `IssuedOn`, `BusinessDate`,
`IdempotencyKey?`. Rules: payment `Accepted`; `Amount ≤ payment.Unallocated` (an earmarked payment may
be refunded directly — a boarding deposit returned under BR-12, reason required); `Method` ∈ the
original method's `AllowedRefundMethods` (else `RefundMethodNotAllowed`). Posting: refund +
`payment.RecordRefund`. Event `RefundIssuedEvent`. FR-ORD-17's record — method, amount, reference,
staff, reason, link to the invoice via the credit note — is complete on the refund and credit note.

### 8.10 Cash-up — US-BIL-12, US-BIL-06 AC3 — ADR-188

**Business date.** A payment's `BusinessDate` is the branch-local date at **acceptance**; a refund's
and a reversal's are the local date at issue. **`CashUpAggregate`** — `BranchId`, `BusinessDate`
(unique pair), `Status` (`Open`, `Closed`), `CountedCash?`, `CountedBy?`, `CountedOn?`, `Variance?`,
`Explanation?` (≤ 1000), `ClosedBy?`, `ClosedOn?`, `ExpectedSnapshot?` (owned, frozen at close: per
method total and count, cash refunds, cash reversals, carried-in amounts, expected cash).

`CashUpCalculator` (pure) over the day's rows at the branch:

```
expectedCash = Σ accepted CashInStore + Σ accepted CashOnDelivery collected by Staff
             − Σ CashInStore refunds − Σ reversals of the above (by ReversalBusinessDate)
             + carriedIn        // late items of earlier closed days (below)
cardTotal, transferTotal       = Σ accepted CardInStore / BankTransfer (listed, not counted)
variance     = countedCash − expectedCash
```

| Method | Rules | Event |
|---|---|---|
| `RecordCount(counted, explanation?)` | `Open`; the date is today or earlier in branch time; recount allowed | — |
| `Close(snapshot, by, now)` | `Open`; counted present; **`variance ≠ 0` → explanation required** (`VarianceExplanationRequired`, US-BIL-12 AC2); **no `PendingApproval` payment at the branch with that `ReceivedOn`/recording date** (`PendingApprovalsExist` — cash is in the drawer but not yet counted as received) | `CashUpClosedEvent` |

**Late items.** A payment accepted concurrently with a close can commit with the closed date. Such
items (accepted after that day's `ClosedOn`) are never lost: the **next** cash-up at the branch
includes them as `carriedIn`, listed individually, and freezes them into its snapshot. A closed
cash-up is terminal (no reopen — OQ-12 asks whether one is needed).

### 8.11 Loyalty — US-BIL-10 — ADR-191

Disabled unless `billing.loyaltyEnabled` is true (default **false**).

- **`LoyaltyAccountAggregate`** (Id == customer id, created on first entry) — `Balance` (int ≥ 0).
- **`LoyaltyEntryAggregate`** (append-only) — `CustomerId`, `Kind` (`Accrual`, `AccrualReversal`,
  `Redemption`, `RedemptionReversal`, `MergeTransferIn`, `MergeTransferOut`), `Points` (> 0),
  `InvoiceId?`, `CreditNoteId?`, `SourceCustomerId?`, `Shortfall` (int, for reversals exceeding the
  balance), `OccurredOn`. Unique `(kind, invoice_id)` and `(kind, credit_note_id)` where not null —
  the idempotency of the handlers.
- **Accrual** — `AccrueLoyaltyOnInvoicePaidHandler` on `InvoiceStatusChangedEvent(To = Paid)`:
  `points = floor(qualifyingNet / billing.loyaltySpendPerPoint)`, where qualifying net = Σ line
  `NetAmount` (after all discounts, before tax) of lines whose `ItemKind ≠ Fee` (OQ-8). Posting:
  account + entry. Across clinic, shop and boarding alike (US-BIL-10 AC1).
- **Reversal** — `ReverseLoyaltyOnCreditNoteIssuedHandler`: `floor(creditedQualifyingNet /
  spendPerPoint)`, capped at the invoice's accrual; if the balance is lower, it goes to 0 and the
  difference is recorded as `Shortfall`.
- **Redemption** — `ApplyLoyaltyRedemptionCommand(InvoiceId, Points)` on a **draft**: `Points ≥
  billing.loyaltyMinimumRedemptionPoints`, `≤ Balance`; value = `Points × billing.loyaltyPointValue`,
  `≤` the draft's qualifying net; posting: draft (`LoyaltyRedemption`) + account + `Redemption`
  entry. `RemoveLoyaltyRedemptionCommand` or discarding the draft posts a `RedemptionReversal`. At issue
  the value is apportioned across qualifying lines pro-rata by net (largest remainder to the largest
  line) **before tax** as `InvoiceDiscountShare`, and printed as a separate *"Loyalty redemption —
  N points"* row (US-BIL-10 AC2).
- These handlers mutate Billing roots only and **never enqueue**.

### 8.12 Messages — ADR-193

Templates appended to HO-NTF-001's catalogue (all `Transactional` — "payment" messages are exempt
from marketing opt-out, FR-NTF-4 — `Normal` urgency, channels `Sms`, `Email`, `InApp`):

| Key | Enqueued by | Producer variables (`Text` unless stated) |
|---|---|---|
| `billing.invoice-issued` | Issue (C8, C9) | `invoiceNumber`, `totalText` (formatted by the producer, e.g. `BDT 1,500.00`), `dueOn` (`Date`), `invoiceLink` (`Url`, every channel) |
| `billing.payment-received` | Record when accepted, Approve | `amountText`, `methodName`, `receiptNumber`, `purposeText` (the earmark label, or the invoice numbers, or *"credit on your account"*), `receiptLink` (`Url`) |
| `billing.refund-issued` | Refund (C19) | `amountText`, `methodName`, `creditNoteNumber` (or *"—"*), `reason` is **not** sent |

`INotificationLinkBuilder` gains `InvoiceLink(invoiceId)` → `{WebBaseUrl}/portal/billing/invoices/{id}`
and `ReceiptLink(paymentId)` → `{WebBaseUrl}/portal/billing/payments/{id}`. Nothing is enqueued for a
draft, a pending payment, a rejection or a reversal (staff-side events; OQ-20 asks about overdue
reminders).

### 8.13 Contracts with other areas — the assumed and the published

**Published** (binding on consumers; changes need an Amended row here):

| Contract | Kind | Consumers |
|---|---|---|
| `IBillableChargeRecorder` + `ChargeRequestDto` + `ChargeSourceKey` | Port (§8.4) | ORD, BRD, SUB |
| `IssueInvoiceFromChargesCommand(InvoiceId, CustomerId, BranchId, Selector, IdempotencyKey)` — `Selector` is exactly one of `ChargeIds[]`, `Source(Kind, Id)`, `BillingGroup(Kind, Id, ServiceDateTo?)`; issues directly (no draft) | Command | ORD (at confirmation), BRD (check-out, per cycle) |
| `RecordPaymentCommand` incl. `CashOnDelivery` + collector, `Earmark(Order\|BoardingStay, id, label)`, allocations | Command | ORD (COD collection, transfer confirmation, POS tender), BRD (deposit) |
| `IssueCreditNoteCommand`, `IssueRefundCommand` | Commands | ORD (returns, cancellations), BRD (early check-out) |
| `PaymentAcceptedEvent(PaymentId, CustomerId, BranchId, Method, Amount, EarmarkKind?, EarmarkId?)`, `PaymentReversedEvent`, `InvoiceIssuedEvent(InvoiceId, CustomerId, BranchId)`, `InvoiceStatusChangedEvent(InvoiceId, From, To)`, `CreditNoteIssuedEvent`, `RefundIssuedEvent` | Integration events — consumer handlers must be idempotent and must not enqueue | ORD (confirm on transfer payment), BRD (confirm on deposit) |
| `GetCustomerBillingSummaryQuery(customerId)`, `GetBillingGroupSummaryQuery(kind, id)` (charges by business line, billed/unbilled, invoiced outstanding, earmarked payments), `GetSourceBillingStatusQuery(kind, id)` | Queries | POR, BRD (spending limit BR-8, dashboard), ORD |
| **`IBillingTransactionScope`** (`RunAsync`, `FlushAsync`) -- Amended 2026-10-09: one database transaction around a producer's change and Billing's commands; Billing owns the savepoint rule and the per-branch numbering lock (ADR-179 amended) | Port | ORD, **BRD, SUB (reuse it)** |
| **`ApprovePaymentCommand`**, **`RejectPaymentCommand`** -- Amended 2026-10-09: published; the approver must differ from the recorder (Billing enforces it); the producer never builds a `UserContext` | Commands | ORD (held transfers), BRD (deposits) |
| **`IBillingRefundPlanner`** (`PlanCreditAsync`, `PlanRefundAsync`) -- Amended 2026-10-09: credit-note line plan and refund draws; replaces a producer reading `IBillingFinder` or Billing's aggregates | Port (read-only) | ORD, BRD |

**Sequencing for producers** (so US-ORD-08's "surfaced for manual reconciliation" holds): (1) charges
are enlisted atomically with the producer's change; (2) the producer's controller then dispatches
`IssueInvoiceFromChargesCommand` (and `RecordPaymentCommand` where money is taken). If step 2 fails,
the charges are **unbilled** and the payment **unallocated** — both appear on Billing's staff lists
(unbilled charges, unallocated credit) and can be issued/allocated by hand. Nothing is silently lost.

**Amended 2026-10-09 (ADR-215, accepted by the owner):** a producer that needs the whole unit to stand or fall together (HO-ORD-001: order, stock, charges, invoice and payment atomic) runs steps (1) and (2) inside `IBillingTransactionScope.RunAsync` instead; then a failure rolls everything back and there is nothing to recover by hand. Billing's numbering is safe inside the scope (ADR-179 amended). The sequencing above remains the contract for producers that do not use the scope.

**Assumed — owned by handovers not yet written** (Phase J; if the real contract differs, stop §20.2):

| Owner | Event Billing subscribes to | Item source port Billing implements | Items |
|---|---|---|---|
| **HO-EMR-001** | `VisitFinalisedEvent(VisitId, CustomerId, CatId, BranchId, AppointmentId?, BoardingStayId?)` | `VisitBillableItemSource` reading the visit's treatments, consumables and diagnostics with **price snapshots and tax class ids recorded by EMR** | `Service` / `Medicine` / `Product`; group = the stay when `BoardingStayId` is set (FR-BRD-15). **Not** the consultation fee (§8.5) |
| **HO-PHM-001** | `PrescriptionDispensedEvent(DispenseId, PrescriptionId, CustomerId, CatId, BranchId, BoardingStayId?)` | `DispenseBillableItemSource` | `Medicine` lines, quantity and unit price snapshot per batch dispensed |
| **HO-SRG-001** | `SurgeryCompletedEvent(SurgeryId, AppointmentId, CustomerId, CatId, BranchId, BoardingStayId?)` | `SurgeryBillableItemSource` | Drugs and consumables used; the **procedure fee** is the appointment's service snapshot (OQ-14 — campaign pricing as a separate ADM service) |

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-177…196** (coordinator-assigned);
ADR-177…194 are used, ADR-195…196 are left free.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-177** | **`Billing` bounded context**, namespace segment `Billing`, schema `billing`, owning the roots of §8.1. It is `.ai/bounded-contexts.md`'s "Invoice" context widened to reconciliation, cash-up, export and loyalty; `.ai/namespace-conventions.md`'s `Invoice`/`Payment` segments collapse into one | Not in the governing docs as named (OQ-1); a reviewer would otherwise split it |
| **ADR-178** | **Charges are the only input to invoices.** Push: `IBillableChargeRecorder` enlists a charge in the producer's unit of work (areas built after Billing). Pull: thin event handlers dispatch `CaptureSourceChargesCommand` in a **fresh DI scope**, swallow and log failures; `GetUncapturedSourcesQuery` + manual re-capture recover. Idempotent by `(source_kind, source_id, source_line_ref)`. Charges are immutable except `Void`; "billed" is derived and made at-most-once by a filtered unique index on `invoice_line.charge_id` | Delivery order puts EMR/PHM/SRG before Billing and ORD/BRD after; two mechanisms are the honest answer. The fresh scope follows from `.ai/domain-events-and-outbox.md` §3 |
| **ADR-179** | **Gapless numbering** per `(branch, financial year, document kind)` via a series row taken and committed in the same `SaveChangesAsync` as the document, `xmin`-guarded, with ≤ 5 retries that reload only the series row. No PostgreSQL sequence, lock or explicit transaction. Format `INV|CN-{branch}-{fy}-{000000}`; FY start from `billing.financialYearStartMonth` | FR-BIL-1 "no gaps, no reuse" under concurrency (US-BIL-01 AC2); a sequence gaps on rollback; the retry looks like a hack unless recorded |
| **ADR-180** | **Issued financial documents are immutable** in the aggregate **and** by PostgreSQL triggers (invoice content columns, invoice lines, credit notes, refunds, allocation entries, payment core columns); status is derived from totals; `Discarded` added for drafts only | BR-11, D-5; "anyone" includes a `psql` prompt (same reasoning as HO-ACC-001 ADR-040); the extra status looks like a deviation from Appendix C |
| **ADR-181** | **Tax**: prices are **tax-exclusive** (FR-ORD-5's formula); the rate is resolved from HO-ADM-001's effective-dated rates **at issue** (tax point = issue, OQ-4) and frozen on the line; tax per line = `round(net × rate / 100, 2, AwayFromZero)`; invoice tax = Σ line tax; invoice-level discounts apportioned pro-rata before tax, largest remainder to the largest line; one currency | US-BIL-07 AC2; rounding choices change totals and must be one decision |
| **ADR-182** | **Payment = a record of money received, method-independent.** `PaymentMethod` carries capability flags; only `PaymentMethodRules` reads identities (U-X4). No online member, token or provider column in v1. The Phase-5 extension plan (§8.8.1). **C-5 guard**: references that look like card numbers are refused and never logged | Owner: gateway later "without rework"; SRS A-2; `.ai/enumeration-pattern.md` forbids a premature online member |
| **ADR-183** | **Attributable manual payments**: recorder, reference, received date mandatory by method; `> billing.paymentApprovalThreshold` → `PendingApproval` until a **different** staff member with `Payments.Approve` approves; no edit — **reversal and re-entry**; reversal releases allocations; a refunded payment cannot be reversed | FR-BIL-12, BR-17, US-BIL-11; the only substitute for a gateway's record |
| **ADR-184** | **Posting exception to rule 1**: issue, allocation, approval, reversal, credit note, refund and loyalty operations commit several Billing roots in **one** `SaveChangesAsync` (atomic, `xmin` on each); secondary roots' events are dispatched by a follow-up no-change `UpdateAsync` (F31) through `BillingPosting.CommitAsync` | Money moving between records must be atomic; rule 1 says "raise it" — this raises it |
| **ADR-185** | **Customer credit = unallocated amounts of accepted payments.** Deposits are earmarked payments; credit-note excess is released from allocations newest-first and becomes credit ("refund as store credit"); cash/transfer refunds draw down a payment; allocation entries are append-only `Allocate`/`Release` | One model for deposits, overpayments and store credit instead of three ledgers; US-BIL-03, US-BIL-08, FR-ORD-17 |
| **ADR-186** | **Credit notes** have their own gapless series, reverse tax at the invoice line's frozen rate, cap per line, and are followed (when a correction is needed) by a corrected draft linked both ways | FR-BIL-5/6, US-BIL-05 AC2 |
| **ADR-187** | **Idempotency at the HTTP edge for money-moving commands**: `Idempotency-Key` header (≤ 100) on record payment, issue invoice (both forms), credit note and refund; unique with the caller; a repeat returns the original result with `200` | ADR-014 moved idempotency to the edge; a double-clicked "Record payment" must not record twice (COM-3, FR-ORD-20 spirit) |
| **ADR-188** | **Cash-up** per branch and business date; business date = local date of acceptance/issue; close requires an explanation for any variance and no pending approvals; closed is terminal; late items carry into the next cash-up | US-BIL-12, US-BIL-06 AC3; the race must be answered, not ignored |
| **ADR-189** | **Reconciliation**: COD remittances with lines per collection (shortfall = collected − Σ remitted); bank statements imported from a canonical CSV, parsed in memory, never stored as files, duplicate imports refused by SHA-256; matches are 1:1 roots with filtered unique indexes, auto-matched only when exactly one candidate has equal amount, date within tolerance and a reference token match; unmatched listed both ways | FR-BIL-7; the only control over money received without a settlement file |
| **ADR-190** | **Accounting export**: a deterministic ZIP of CSVs (fixed entry order and timestamp, invariant culture, UTF-8 without BOM, `\n`), containing only immutable facts dated in the range; every row has `recordType` and a stable `documentId`; each run is recorded (who, range, counts, SHA-256) by a separate command; CSV-injection guard as HO-ADM-001 ADR-051 | FR-BIL-10, US-BIL-09 AC2 "identical output" — current status would change between runs |
| **ADR-191** | **Loyalty**: disabled by default; accrue on `Paid`, reverse on credit, redeem on drafts as a pre-tax discount; account + append-only entries; handlers idempotent by unique keys | US-BIL-10 with the fewest moving parts |
| **ADR-192** | **Merge and anonymisation**: immutable financial records keep the original customer id and reads fold in customers merged into the survivor; mutable records (unbilled charges, drafts, loyalty balance) are moved; the invoice's `BillToSnapshot` is retained under D-9 after anonymisation | HO-ACC-001 ADR-039 is binding, but rewriting an issued invoice would break ADR-180 |
| **ADR-193** | **Billing messages** (three templates) are enqueued in command handlers only; none for staff-internal states | HO-NTF-001 ADR-057 |
| **ADR-194** | **Money** follows HO-ADM-001 ADR-054 (`numeric(12,2)` + code) — exact in minor units, so SRS C-6's intent is met; `.ai/persistence-ef-core.md`'s `numeric(19,4)` is not adopted; all arithmetic in `InvoiceCalculator` with explicit rounding | Three documents disagree (F33); the builder must not pick one silently |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` / namespace conventions: rename "Invoice" to `Billing` | Architect | ADR-177 records it |
| OQ-2 | Financial-year start month | Accountant | **7** (July — Bangladesh FY) |
| OQ-3 | Are catalogue prices tax-inclusive or exclusive? (SRS Q-5) | Accountant | Exclusive (FR-ORD-5) |
| OQ-4 | Tax point — issue date or supply date? | Accountant | Issue date |
| OQ-5 | Approval threshold value; may `Reception` approve? | Owner | **50 000.00 BDT**; Accountant and Administrator only |
| OQ-6 | Payment terms (due date) | Owner | Due on issue (0 days) |
| OQ-7 | Gift vouchers: how sold, redeemed, expiry? | Owner | Not built |
| OQ-8 | Loyalty: rates, qualifying lines (medicines? fees?), expiry | Owner | 1 point per 100.00 spent, 1 point = 1.00, min 100 to redeem, fees excluded, no expiry |
| OQ-9 | Target accounting system (SRS Q-13) | Accountant | Canonical CSV ZIP (§8.10 of ADR-190) |
| OQ-10 | Bank statement format / bank | Accountant | Canonical CSV: `date,amount,direction,reference,description,bankTransactionId` |
| OQ-11 | Courier master data (SRS Q-4) | Owner / HO-ORD-001 | Courier code + name snapshot on the payment |
| OQ-12 | "Branch manager" is not an Appendix D role — who closes the cash-up; is reopening allowed? | Owner | Reception counts; Accountant/Administrator close; no reopen |
| OQ-13 | Card terminal settlement reconciliation (SRS Q-3c) | Owner | Not built; card totals listed in the cash-up |
| OQ-14 | Who charges the surgery procedure fee; how is campaign pricing (US-SRG-11) expressed? | Owner + HO-SRG-001 | The appointment's service snapshot; campaign = a separate ADM service with its own price |
| OQ-15 | Manual discounts at the desk (other than producer line discounts and loyalty)? | Owner | Only on manual charges, as a line discount, `Billing.ManageCharges` |
| OQ-16 | Refund method per original method | Accountant | §8.8.1 table |
| OQ-17 | Money representation tension (C-6 vs ADR-054 vs `.ai`) | Architect | ADR-194 |
| OQ-18 | Many-to-one bank matches (one statement line ↔ several payments) | Accountant | 1:1 only; staff record one payment per bank credit |
| OQ-19 | Scheduled accounting export | Accountant | On demand only |
| OQ-20 | Overdue-balance reminders to customers | Owner | Not sent |
| OQ-21 | Personal-data export (US-ACC-13) should include invoices | Owner / HO-ACC | Not changed here |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `BusinessLine` | `Clinic`, `Shop`, `Boarding` |
| `LineItemKind` | `Service`, `Product`, `Medicine`, `BoardingNight`, `Fee` |
| `ChargeSourceKind` | `Appointment`, `Visit`, `Dispense`, `Surgery`, `Order`, `BoardingStay`, `Manual` |
| `ChargeStatus` | `Active`, `Voided` |
| `BillingGroupKind` | `BoardingStay` |
| `InvoiceStatus` | `Draft`, `Issued`, `PartPaid`, `Paid`, `Credited`, `Discarded` |
| `DocumentKind` | `Invoice`, `CreditNote` |
| `PaymentMethod` | `CashInStore`, `CardInStore`, `BankTransfer`, `CashOnDelivery` — with the §8.8.1 flags |
| `PaymentStatus` | `PendingApproval`, `Accepted`, `Rejected`, `Reversed` |
| `CollectorKind` | `Staff`, `Courier` |
| `EarmarkKind` | `Order`, `BoardingStay` |
| `AllocationEntryKind` | `Allocate`, `Release` |
| `AllocationReleaseReason` | `CreditNoteExcess`, `PaymentReversed` |
| `CreditNoteKind` | `Correction`, `Return`, `Cancellation`, `Goodwill` |
| `RefundMethod` | `CashInStore`, `BankTransfer` |
| `CashUpStatus` | `Open`, `Closed` |
| `RemittanceMethod` | `Cash`, `BankTransfer` |
| `BankEntryDirection` | `Credit`, `Debit` |
| `BankMatchMethod` | `Auto`, `Manual` |
| `LoyaltyEntryKind` | `Accrual`, `AccrualReversal`, `Redemption`, `RedemptionReversal`, `MergeTransferIn`, `MergeTransferOut` |
| `AgeingBucket` | `Current`, `Days1To30`, `Days31To60`, `Days61To90`, `Over90` — **computed only** |

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions` (new group `Billing`)

Already in the registry, consumed here: `commerce.currencyCode`.

| Key | Type | Default | Bounds | Branch override |
|---|---|---|---|---|
| `billing.financialYearStartMonth` | Integer | 7 | 1–12 | ❌ |
| `billing.paymentTermsDays` | Integer | 0 | 0–120 | ✅ |
| `billing.paymentApprovalThreshold` | Decimal | 50000.00 | 0–100 000 000 | ✅ |
| `billing.bankMatchDateToleranceDays` | Integer | 3 | 0–14 | ❌ |
| `billing.codRemittanceDueDays` | Integer | 7 | 1–60 | ❌ |
| `billing.loyaltyEnabled` | Boolean | false | — | ❌ |
| `billing.loyaltySpendPerPoint` | Decimal | 100.00 | 1–100 000 | ❌ |
| `billing.loyaltyPointValue` | Decimal | 1.00 | 0.01–1 000 | ❌ |
| `billing.loyaltyMinimumRedemptionPoints` | Integer | 100 | 1–100 000 | ❌ |

`billing.financialYearStartMonth` **must not change once any invoice exists** — the handler of
HO-ADM-001's settings change is not edited; instead `DocumentNumbering` refuses to issue when the
computed FY for today would re-enter a series already closed (`FinancialYearConfigurationChanged`) —
U-C9. No migration (values are rows).

### 10.3 Other constants

`BillingRules` (`MaxInvoiceLines = 500`, `MaxNumberingAttempts = 5`, `MaxBackdatedReceiptDays = 31`,
`MaxAllocationEntries = 200`, `MaxStatementLines = 5000`, `MaxStatementBytes = 1_048_576`,
`MaxExportRangeDays = 366`, `MaxExportRows = 200_000`) · `ChargeRules` · `PaymentMethodRules` ·
`CardNumberGuard` · `FinancialYearRules` · `DocumentNumberFormat` · `InvoiceStatusRules` ·
`BillingMessages` (every two-path message) · the three template definitions (§8.12).

### 10.4 Violation codes — append to `BusinessViolationCodes`

`CurrencyMismatch` · `ChargeNotFound` · `ChargeAlreadyRecorded` · `ChargeAlreadyInvoiced` ·
`ChargeNotActive` · `ChargesMixedCustomerOrBranch` · `TaxClassNotFound` · `TaxRateNotConfigured` ·
`InvalidQuantity` · `DiscountExceedsAmount` · `InvoiceNotFound` · `InvoiceNotDraft` ·
`InvoiceTooManyLines` · `BillingProfileMissing` · `FinancialYearConfigurationChanged` ·
`AllocationExceedsOutstanding` · `AllocationExceedsUnallocated` · `EarmarkMismatch` ·
`PaymentNotFound` · `PaymentNotAccepted` · `PaymentNotPending` · `ApproverMustDiffer` ·
`ReferenceRequired` · `CollectorRequired` · `ReferenceLooksLikeCardNumber` ·
`ReceivedDateInFuture` · `ReceivedDateTooOld` · `PaymentHasRefunds` · `ReasonRequired` (existing,
reused) · `CreditExceedsInvoice` · `CreditExceedsLine` · `CreditNoteNotFound` ·
`RefundMethodNotAllowed` · `RefundExceedsAvailable` · `CashUpClosed` · `CashUpDateInFuture` ·
`CountRequired` · `VarianceExplanationRequired` · `PendingApprovalsExist` · `CollectionNotCod` ·
`CollectionCourierMismatch` · `RemittanceExceedsCollection` · `StatementDuplicate` ·
`StatementTooLarge` · `StatementMalformed` · `StatementLineNotFound` · `BankLineAlreadyMatched` ·
`PaymentAlreadyMatched` · `MatchAmountMismatch` · `MatchMethodNotTransfer` · `ExportRangeTooLarge` ·
`LoyaltyNotEnabled` · `LoyaltyInsufficientPoints` · `LoyaltyBelowMinimum` ·
`LoyaltyRedemptionExceedsNet` · `IdempotencyKeyConflict`.

`RefundExceedsAvailable` and `AllocationExceedsOutstanding` name the available amount in the message
(*"Only BDT 400.00 is outstanding on INV-MAIN-2627-000123."*) — NFR-USA-4.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Billing/`

Namespace `CatEcommerce.Application.Commands.Billing`. Data only; ids minted in the controller;
`ValidateCommand = true` everywhere (F3).

| # | Command | Sent by | Story | Phase |
|---|---|---|---|---|
| C1 | `SetBillingProfileCommand` (`BranchId`, `LegalName`, `TaxRegistrationNumber`, `AddressLines`, `Phone?`, `Email?`, `InvoiceFooter?`, `BankAccounts[]` (`BankName`, `AccountName`, `AccountNumber`, `BranchName?`, `RoutingNumber?`)) | Admin | BIL-02 (NFR-LEG-3) | A |
| C2 | `RecordManualChargeCommand` (`ChargeId`, `CustomerId`, `CatId?`, `ServiceId?` **or** (`Description`, `UnitPrice`, `TaxClassId`, `ItemKind = Fee`), `Quantity`, `LineDiscount?`, `ServiceDate`) — branch from the active branch; a service's price is read **at now** (HO-ADM-001 ADR-049) | Staff | BIL-02 | B |
| C3 | `VoidChargeCommand` (`ChargeId`, `Reason`) | Staff | BIL-02 | B |
| C4 | `CaptureSourceChargesCommand` (`SourceKind`, `SourceId`) | Event handler, staff | BIL-02 | B (Appointment), J |
| C5 | `CreateDraftInvoiceCommand` (`InvoiceId`, `CustomerId`, `ChargeIds[]`, `Notes?`) | Staff | BIL-02, ORD-17 | C |
| C6 | `SetDraftInvoiceChargesCommand` (`InvoiceId`, `ExpectedVersion`, `ChargeIds[]`) · `DiscardDraftInvoiceCommand` (`InvoiceId`) | Staff | BIL-02 | C |
| C8 | `IssueInvoiceCommand` (`InvoiceId`, `ExpectedVersion`, `IdempotencyKey?`) | Staff | BIL-01, 07 | C |
| C9 | `IssueInvoiceFromChargesCommand` (`InvoiceId`, `CustomerId`, `BranchId?`, `Selector`, `IdempotencyKey?`) | Staff, ORD, BRD | BIL-01; contract | C |
| C10 | `RecordPaymentCommand` (`PaymentId`, `CustomerId`, `Method`, `Amount`, `ReceivedOn`, `ExternalReference?`, `Collector?`, `Earmark?`, `Allocations[]?`, `Notes?`, `IdempotencyKey?`) | Staff, ORD, BRD | BIL-11, 03, 04 | D |
| C11 | `ApprovePaymentCommand` (`PaymentId`) · `RejectPaymentCommand` (`PaymentId`, `Reason`) | Approver | BIL-11 AC2 | D |
| C12 | `AllocatePaymentCommand` (`PaymentId`, `Allocations[]` (`InvoiceId`, `Amount`)) | Staff | BIL-03, 04 | D |
| C13 | `ApplyCustomerCreditCommand` (`CustomerId`, `InvoiceId`, `Amount?` — null = as much as possible) — draws free (non-earmarked) credit from payments **oldest first** | Staff | BIL-08 | I |
| C14 | `ReleasePaymentEarmarkCommand` (`PaymentId`, `Reason`) | Staff | BIL-03 | D |
| C15 | `ReversePaymentCommand` (`PaymentId`, `Reason`) | Accountant | BIL-11 AC3 | D |
| C16 | `IssueCreditNoteCommand` (`CreditNoteId`, `InvoiceId`, `Kind`, `Reason`, `FullCredit`, `Lines[]?` (`InvoiceLineId`, `Quantity?` \| `Amount?`), `IdempotencyKey?`) | Staff | BIL-05 AC2 | E |
| C17 | `CreateCorrectedDraftCommand` (`InvoiceId`, `CreditNoteId`) · C18 `SetCorrectedDraftLinesCommand` (`InvoiceId`, `ExpectedVersion`, `Lines[]`) | Staff | BIL-05 AC2 | E |
| C19 | `IssueRefundCommand` (`RefundId`, `PaymentId`, `CreditNoteId?`, `Method`, `Amount`, `Reference?`, `Reason`, `IdempotencyKey?`) | Staff | FR-BIL-5, ORD-15 | E |
| C20 | `RecordCashCountCommand` (`BusinessDate`, `CountedCash`, `Explanation?`) · C21 `CloseCashUpCommand` (`BusinessDate`, `ExpectedVersion`) — branch from the active branch | Staff | BIL-12, BIL-06 AC3 | F |
| C22 | `RecordCodRemittanceCommand` (`RemittanceId`, `CourierCode`, `ReceivedOn`, `Amount`, `Method`, `Reference?`, `Lines[]` (`PaymentId`, `AmountRemitted`)) | Accountant | BIL-06 AC1 | G |
| C23 | `ImportBankStatementCommand` (`StatementId`, `BankAccountLabel`, `PeriodFrom`, `PeriodTo`, `SourceFileName`, `SourceSha256`, `Lines[]`) — lines parsed by the controller through `IBankStatementCsvParser` | Accountant | BIL-06 AC2 | G |
| C24 | `AutoMatchBankStatementCommand` (`StatementId`) · C25 `MatchBankLineCommand` (`MatchId`, `StatementLineId`, `PaymentId`) · `UnmatchBankLineCommand` (`MatchId`, `Reason`) | Accountant | BIL-06 AC2 | G |
| C26 | `RecordAccountingExportRunCommand` (`RunId`, `From`, `To`, `BranchId?`, `RowCounts`, `Sha256`) | Accountant | BIL-09 | H |
| C27 | `ApplyLoyaltyRedemptionCommand` (`InvoiceId`, `Points`) · `RemoveLoyaltyRedemptionCommand` (`InvoiceId`) | Staff | BIL-10 AC2 | I |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty; amounts `> 0` (charges and unit prices `≥ 0`) with ≤ 2 decimals; quantity
`> 0` with ≤ 3 decimals; text lengths; enum membership; `Selector` has exactly one form; `Lines` count
bounds; `Reason` non-blank where required; `BusinessDate`/`ReceivedOn` not more than one day ahead
**by UTC** (the precise branch-local rule is the handler's). Method-dependent requirements (reference,
collector), thresholds, caps and card-number checks are aggregate or handler rules.

### 11.3 Mappers and DTOs

`BillingMapper` (static `ToDomainDto()` extensions). DTOs in `Domain/Models/` (flat):
`ChargeRequestDto`, `ChargeSourceKey`, `DraftInvoiceDto`, `InvoiceIssueDto`, `PaymentRecordDto`,
`AllocationRequest`, `CreditNoteIssueDto`, `RefundIssueDto`, `CashUpSnapshot`, `CodRemittanceDto`,
`BankStatementLineInput`, `LoyaltyRedemptionDto`, `SellerSnapshot`, `BillToSnapshot`.

### 11.4 Command handlers — `CommandHandlers/Billing/`

Every posting handler ends with `BillingPosting.CommitAsync` (ADR-184). Every issuing handler wraps
its save in `IDocumentNumbering.SaveWithRetryAsync` (ADR-179). Every handler that takes an
`IdempotencyKey` first looks up `(caller, key)`; a hit with the same command type returns the stored
result (`200`), a hit with a different target returns `IdempotencyKeyConflict` (`409`) (ADR-187).

| Handler | Specifics |
|---|---|
| **C1** | Branch exists; load-or-create by id == branch id; card-number guard does **not** apply to bank account numbers (they are the business's own) |
| **C2** | Customer exists and is active (HO-ACC-001 repository); `ServiceId` → service active and priced, price and tax class **at now**, description = service name, `ItemKind = Service`; charge via `IBillableChargeRecorder` then `SaveChangesAsync` through the charge repository (`SourceKind = Manual`, `SourceId = ChargeId`) |
| **C3** | Charge `Active`; not referenced by a non-discarded invoice line (query) → `ChargeAlreadyInvoiced` |
| **C4** | Resolve `IBillableItemSource` for the kind (§8.13); source must be in a billable state (appointment `Completed`, …) else success with `capturedCount: 0, reason`; record missing items one `SaveAsync` each; return `{ captured, skipped[] }` |
| **C5/C6** | Load charges; all `Active`, same customer and branch (`ChargesMixedCustomerOrBranch`), currency; not billed (pre-check) — the filtered unique index `ix_invoice_line_charge` is the race guard (`23505` → `409 ChargeAlreadyInvoiced`) |
| **C8** | `ExpectedVersion`; load `BillingProfile(branch)`, tax rates at now for every class (`IBillingPricingInputs`), customer for `BillToSnapshot`; FY from branch zone and setting; `EnsureSeries` → `Take` → `invoice.Issue(...)`; enqueue `billing.invoice-issued`; save with numbering retry |
| **C9** | Resolve the selector to unbilled `Active` charges of the customer (`Source`: that source key; `BillingGroup`: that group with `ServiceDate ≤ ServiceDateTo`); none → `ChargeNotFound`; create the draft and issue it **in the same save** |
| **C10** | Method flags; `ReceivedOn` rules in branch time; card-number guard on reference and notes; customer exists; `Collector.StaffAccountId` exists when `Staff`; earmark source not validated against ORD/BRD tables (they may not exist yet) but required fields present; threshold via `IBusinessSettings` for the branch; accepted → allocations applied (each invoice loaded, same customer or a customer merged into it, issued state), `BusinessDate` set, enqueue `billing.payment-received`; pending → requested allocations stored, nothing applied, nothing enqueued |
| **C11** | Approver holds `Payments.Approve` (filter) and ≠ recorder (aggregate); approve → apply requested allocations capped at outstanding, enqueue receipt message; the response lists `applied[]` and `leftUnallocated` |
| **C12/C13** | As §8.8; C13 picks free-credit payments oldest `AcceptedOn` first until the amount or the outstanding is reached |
| **C15** | Reason; payment `Accepted`, `RefundedTotal == 0`; release every effective allocation (invoices loaded); `ReversalBusinessDate` = today local |
| **C16** | §8.9 steps 1–4 |
| **C17/C18** | Source invoice has the credit note; corrected draft lines validated like charges |
| **C19** | §8.9 refund rules; enqueue `billing.refund-issued` |
| **C20/C21** | Load-or-create cash-up for `(branch, date)`; close computes the snapshot through `CashUpCalculator` from a query of the day's payments, refunds, reversals and carried-in items; pending-approvals check by query |
| **C22** | Each line's payment: `CashOnDelivery`, `Accepted`, `Collector.Kind == Courier`, `CourierCode` equal (`CollectionCourierMismatch`); Σ remitted per payment across all remittances ≤ payment amount (`RemittanceExceedsCollection`); `Amount ≥ Σ lines` (the difference is recorded as unapplied) |
| **C23** | Duplicate `(bank_account_label, source_sha256)` → `StatementDuplicate`; lines ≤ 5000; descriptions card-masked |
| **C24** | Load statement credit lines without an active match and candidate transfer payments (accepted, unmatched, `ReceivedOn` in `[PeriodFrom − tol, PeriodTo + tol]`); `BankMatchSuggester` (pure) returns lines with exactly one candidate; one `SaveAsync` per match; unique indexes make a concurrent re-run safe (`23505` → skip) |
| **C25** | Amounts equal (`MatchAmountMismatch`), payment `BankTransfer` and `Accepted` (`MatchMethodNotTransfer`), line is a credit; unmatch keeps the row with `UnmatchedOn/By/Reason` |
| **C26** | Writes one `AccountingExportRunAggregate` and one `BusinessEvent` audit row |
| **C27** | §8.11 |

### 11.5 Domain events — `Events/Billing/`

Ids, instants, dates, enums, booleans and **decimal amounts** only — **no strings** (no reasons,
references, names, labels). U-X1 reflects over the assembly.

`BillingProfileChangedEvent`, `DraftInvoiceCreatedEvent`, `DraftInvoiceChangedEvent`,
`DraftInvoiceDiscardedEvent`, **`InvoiceIssuedEvent`** (`InvoiceId`, `CustomerId`, `BranchId`,
`Total`), **`InvoiceStatusChangedEvent`** (`InvoiceId`, `From`, `To`), `PaymentRecordedEvent`,
`PaymentApprovedEvent`, `PaymentRejectedEvent`, **`PaymentAcceptedEvent`** (§8.13),
`PaymentAllocatedEvent`, `PaymentAllocationReleasedEvent`, `PaymentEarmarkReleasedEvent`,
**`PaymentReversedEvent`**, **`CreditNoteIssuedEvent`** (`CreditNoteId`, `InvoiceId`, `Total`,
`ReleasedToCustomerCredit`), **`RefundIssuedEvent`**, `CashUpClosedEvent` (`BranchId`,
`BusinessDate`, `Variance`), `CodRemittanceRecordedEvent`, `BankStatementImportedEvent`,
`BankLineMatchedEvent`, `BankLineUnmatchedEvent`, `LoyaltyPointsAccruedEvent`,
`LoyaltyPointsRedeemedEvent`, `AccountingExportRecordedEvent`.

### 11.6 Domain event handlers — `DomainEventHandlers/Billing/`

| Handler | Handles | Does |
|---|---|---|
| `CaptureChargesOnAppointmentCompletedHandler` | HO-APT-001 `AppointmentCompletedEvent` | §8.5 — fresh scope, C4, swallow + log |
| `CaptureChargesOnVisitFinalisedHandler` · `…OnPrescriptionDispensedHandler` · `…OnSurgeryCompletedHandler` | §8.13 assumed events | Phase J, same shape |
| `AccrueLoyaltyOnInvoicePaidHandler` · `ReverseLoyaltyOnCreditNoteIssuedHandler` | Billing events | §8.11; no-op when disabled |
| `ReassignBillingOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | Unbilled `Active` charges and **drafts** of the duplicate → survivor (one `UpdateAsync` each); loyalty balance moved by `MergeTransferOut`/`In` entries (idempotent by `SourceCustomerId`); issued invoices, payments, credit notes, refunds untouched (ADR-192) |
| `AuditBillingDecisionHandler` | `PaymentApprovedEvent`, `PaymentRejectedEvent`, `PaymentReversedEvent`, `PaymentEarmarkReleasedEvent`, `CreditNoteIssuedEvent`, `RefundIssuedEvent`, `CashUpClosedEvent`, `BankLineUnmatchedEvent` | Loads the aggregate by id and writes a `BusinessEvent` audit row carrying the **reason / explanation** and the actor (the reason lives here, not in the event — HO-ADM-001 §11.6's pattern). Charge voids are audited by C3 directly |

None of them enqueue. Data changes are audited by HO-ACC-001's interceptor in the same transaction.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Eighteen `DbSet`s; one configuration per root; schema `billing`; HO-ACC-001 §12.1 conventions (F15,
`ValueGeneratedNever()` on every root and owned id — ADR-021); `Money` as `amount numeric(12,2)` +
`currency_code char(3)`; quantity `numeric(10,3)`; tax rate `numeric(5,2)`; enumerations by `Value`;
`receipt_number bigint` with `HasDefaultValueSql("nextval('billing.receipt_number_seq')")` and
`ValueGeneratedOnAdd`.

| Index / constraint | Kind |
|---|---|
| `charge (source_kind, source_id, source_line_ref)` | **UNIQUE** `ix_charge_source` |
| `charge (customer_id, status)`, `(billing_group_kind, billing_group_id, service_date)`, `(branch_id, service_date)` | unbilled lists, group selection |
| `invoice_line (charge_id)` **UNIQUE** filtered `WHERE charge_id IS NOT NULL AND NOT invoice_discarded` | `ix_invoice_line_charge` (`invoice_discarded` is a denormalised boolean on the line, set by `Discard` before the lines leave scope — it is the only line column a draft may change) |
| `invoice (number)` **UNIQUE** filtered not null; `(branch_id, financial_year, sequence_number)` **UNIQUE** filtered | `ix_invoice_number`, `ix_invoice_sequence` |
| `invoice (customer_id, status)`, `(branch_id, issue_date)`, `(status, due_on)` | balances, lists, ageing |
| `document_number_series (branch_id, financial_year, document_kind)` | **UNIQUE** |
| `credit_note (number)` **UNIQUE**; `credit_note (invoice_id)` | |
| `payment (recorded_by, idempotency_key)` **UNIQUE** filtered not null | `ix_payment_idempotency` (same shape on invoice issue, credit note, refund) |
| `payment (customer_id, status)`, `(branch_id, business_date)`, `(method, status, received_on)`, `(earmark_kind, earmark_source_id)`, `(status, recorded_on)` | balances, cash-up, bank candidates, earmarks, approvals |
| `payment_allocation_entry (invoice_id)` | invoice payment history |
| `cash_up (branch_id, business_date)` | **UNIQUE** |
| `cod_remittance_line (payment_id)` | reconciliation |
| `bank_statement (bank_account_label, source_sha256)` | **UNIQUE** `ix_bank_statement_source` |
| `bank_match (statement_line_id)` and `bank_match (payment_id)` **UNIQUE** filtered `WHERE unmatched_on IS NULL` | `ix_bank_match_line`, `ix_bank_match_payment` |
| `loyalty_entry (kind, invoice_id)`, `(kind, credit_note_id)`, `(kind, source_customer_id)` **UNIQUE** filtered not null | handler idempotency |

**Triggers** (ADR-180, hand-written in the migration, status values from `Enumeration.Value`):
`trg_invoice_immutable`, `trg_invoice_line_immutable`, `trg_credit_note_immutable` (+ lines),
`trg_refund_immutable`, `trg_payment_allocation_entry_immutable`, `trg_payment_immutable` (core
columns: amount, currency, method, reference, received_on, collector, recorded_by, recorded_on,
receipt_number; `DELETE` always), `trg_bank_statement_line_immutable`,
`trg_accounting_export_run_immutable`, `trg_loyalty_entry_immutable`. **No Billing table accepts
`DELETE`** (D-6), except `invoice_line` of a `Draft` invoice.

**Migrations**, each read before commit: `AddBillingFoundation` (A — schema, profile, series,
receipt sequence), `AddBillingCharges` (B), `AddBillingInvoices` (C — with triggers),
`AddBillingPayments` (D — with triggers), `AddBillingCreditNotesAndRefunds` (E), `AddBillingCashUp`
(F), `AddBillingReconciliation` (G), `AddBillingAccountingExport` (H), `AddBillingLoyalty` (I).
Phase J adds none.

### 12.2 Infrastructure services

| Class | Notes |
|---|---|
| `Billing/EfBillableChargeRecorder : IBillableChargeRecorder` | §8.4; checks `ChangeTracker` and the table for the source key |
| `Billing/DocumentNumbering : IDocumentNumbering` | §8.7; `EnsureSeriesAsync` raw `INSERT … ON CONFLICT DO NOTHING`; `SaveWithRetryAsync` |
| `Billing/BillingPricingInputs : IBillingPricingInputs` | Read-only, `AsNoTracking`: tax rate at *t* per class, tax class code, service price at *t* — over `admin.*` (HO-ADM-001 §8.6's rule) |
| `Billing/ItemSources/AppointmentBillableItemSource : IBillableItemSource` | §8.5; reads `clinic.appointment` and `admin.clinic_service` |
| `Billing/ItemSources/{Visit,Dispense,Surgery}BillableItemSource` | Phase J, §8.13 |
| `Billing/BankStatementCsvParser : IBankStatementCsvParser` | Canonical CSV (OQ-10): header required, RFC 4180 quoting, dates `yyyy-MM-dd`, amounts invariant with `.` and ≤ 2 decimals, direction `C`/`D`; rejects files > 1 MiB, non-UTF-8, formulas are kept as text; returns row-numbered errors (`StatementMalformed` naming the first 10 rows) |
| `Billing/Documents/QuestPdfInvoiceRenderer`, `QuestPdfCreditNoteRenderer` | Seller and bill-to snapshots, lines with tax per line, business line column, totals, payments applied, loyalty row, bank details, footer; `IssuedOn` in the branch zone. **No customer-supplied text rendered outside a text span** |
| `Billing/AccountingExportWriter` | ADR-190; streams the ZIP; computes SHA-256 while writing |
| `Notifications/NotificationLinkBuilder` | `InvoiceLink`, `ReceiptLink` added only |

### 12.3 Authorization

Appendix D — "Invoices & payments": Customer **R (own)**, Reception **W**, Shop Mgr **W**, Accountant
**F**, Admin **F**; "Refunds & credit notes": Shop Mgr **W**, Accountant **F**, Admin **F**; "Audit
log": Accountant R. Appended to the one `ActionRoles` table:

| Action | Staff roles |
|---|---|
| `Billing.View` | Reception, ShopManager, Accountant, Administrator |
| `Billing.ManageCharges` | Reception, ShopManager, Accountant, Administrator |
| `Billing.DraftInvoices` · `Billing.IssueInvoices` | Reception, ShopManager, Accountant, Administrator |
| `Payments.Record` · `Payments.Allocate` | Reception, ShopManager, Accountant, Administrator |
| `Payments.Approve` | Accountant, Administrator (OQ-5) |
| `Payments.Reverse` | Accountant, Administrator |
| `CreditNotes.Issue` · `Refunds.Issue` | ShopManager, Accountant, Administrator |
| `CashUp.Count` | Reception, Accountant, Administrator |
| `CashUp.Close` | Accountant, Administrator (OQ-12) |
| `Reconciliation.Manage` | Accountant, Administrator |
| `AccountingExport.Run` | Accountant, Administrator |
| `BillingProfile.Manage` | Administrator |

Customer endpoints carry `[RequireCustomerSession]` and resolve every invoice and payment **by id and
caller** (caller or a customer merged into the caller): someone else's is **`404`**. Drafts, pending
and rejected payments are never visible to customers.

### 12.4 Api — the contract HO-BIL-002 consumes

JSON camelCase; money as `{ "amount": "1500.00", "currencyCode": "BDT" }` (amount a **string**,
HO-ADM-001 §12.4); quantities as strings (`"2.000"`); dates `yyyy-MM-dd`; instants ISO-8601 with
offset; every document view carries `branchTimeZoneId`. Money-moving `POST`s accept
`Idempotency-Key`. Staff branch from `X-Branch-Id`.

**Configuration and reference**

| Method | Route | Access | → |
|---|---|---|---|
| `GET` | `/api/billing/payment-methods` | `Billing.View` | `[{ value, name, countsInTill, requiresReference, referenceLabel, requiresCollector, isOnline, allowedRefundMethods[] }]` |
| `GET` / `PUT` | `/api/admin/billing-profiles/{branchId}` | `Billing.View` / `BillingProfile.Manage` | profile / C1 → `204` |

**Charges and invoices (staff)**

| Method | Route | Access | → |
|---|---|---|---|
| `GET` | `/api/staff/billing/customers/{customerId}/summary` | `Billing.View` | `{ outstanding, overdue, freeCredit, earmarkedCredit: [{ paymentId, label, amount }], unbilledTotal, loyaltyPoints?, oldestDueOn? }` — **US-BIL-03 AC1** |
| `GET` | `/api/staff/billing/charges?customerId&status=unbilled\|billed\|voided&sourceKind&billingGroupId&pageNumber&pageSize` | `Billing.View` | paged charges with `billedOnInvoice?` |
| `POST` | `/api/staff/billing/charges` | `Billing.ManageCharges` | C2 → `201 { chargeId }` |
| `POST` | `/api/staff/billing/charges/{chargeId}/void` | `Billing.ManageCharges` | C3 → `204` |
| `GET` | `/api/staff/billing/uncaptured-sources?from&to` | `Billing.View` | `[{ sourceKind, sourceId, customerId, completedOn, label }]` |
| `POST` | `/api/staff/billing/sources/{sourceKind}/{sourceId}/capture` | `Billing.ManageCharges` | C4 → `200 { captured, skipped: [{ reason }] }` |
| `POST` | `/api/staff/billing/invoices/preview` | `Billing.View` | `{ customerId, chargeIds[], loyaltyPoints? }` → lines and totals at **today's** rates, `isEstimate: true` — **no write** |
| `POST` | `/api/staff/billing/invoices` | `Billing.DraftInvoices` | C5 → `201 { invoiceId }` |
| `PUT` | `/api/staff/billing/invoices/{id}/charges` | `Billing.DraftInvoices` | C6 → `200 { invoice }`; `409` stale |
| `POST` | `/api/staff/billing/invoices/{id}/discard` | `Billing.DraftInvoices` | `204` |
| `POST` / `DELETE` | `/api/staff/billing/invoices/{id}/loyalty-redemption` | `Billing.DraftInvoices` | C27 → `200 { invoice }` |
| `POST` | `/api/staff/billing/invoices/{id}/issue` | `Billing.IssueInvoices` | C8 → `200 { invoiceId, number }` |
| `POST` | `/api/staff/billing/invoices/issue-from-charges` | `Billing.IssueInvoices` | C9 → `201 { invoiceId, number }` |
| `GET` | `/api/staff/billing/invoices?customerId&status&number&from&to&businessLine&pageNumber&pageSize` · `/export.csv` | `Billing.View` | paged list / streamed CSV (UI-4; injection-guarded) |
| `GET` | `/api/staff/billing/invoices/{id}` | `Billing.View` | detail: header, seller/bill-to snapshots, lines (business line, item kind, qty, unit price, discounts, tax class code, **rate**, net, tax, gross), totals, `outstanding`, payments applied (`receiptNumber`, method, amount, on), credit notes, corrected-by/corrects links, `allowedActions[]` |
| `GET` | `/api/staff/billing/invoices/{id}.pdf` · `/api/staff/billing/credit-notes/{id}.pdf` | `Billing.View` | `application/pdf`, `Cache-Control: no-store` |
| `POST` | `/api/staff/billing/invoices/{id}/credit-notes` | `CreditNotes.Issue` | C16 → `201 { creditNoteId, number, releasedToCustomerCredit }` |
| `POST` | `/api/staff/billing/invoices/{id}/corrected-draft` | `Billing.DraftInvoices` | C17 `{ creditNoteId }` → `201 { invoiceId }` |
| `PUT` | `/api/staff/billing/invoices/{id}/lines` | `Billing.DraftInvoices` | C18 (corrected drafts only) → `200` |
| `GET` | `/api/staff/billing/credit-notes/{id}` | `Billing.View` | detail |

**Payments, credit, refunds (staff)**

| Method | Route | Access | → |
|---|---|---|---|
| `POST` | `/api/staff/billing/payments` | `Payments.Record` | C10 → `201 { paymentId, status, receiptNumber?, applied: [{ invoiceId, number, amount }], unallocated, invoiceOutstanding[] }`; repeat key → `200` same body |
| `GET` | `/api/staff/billing/payments?customerId&status&method&from&to&pageNumber&pageSize` · `/export.csv` | `Billing.View` | paged |
| `GET` | `/api/staff/billing/payments/{id}` | `Billing.View` | detail incl. entries, refunds, approval trail, `allowedActions[]` |
| `GET` | `/api/staff/billing/payments/{id}/receipt` | `Billing.View` | print data: seller snapshot of the branch profile, receipt number, customer name, method, reference, amount, received on, applied to, recorded by (name) |
| `GET` | `/api/staff/billing/approvals` | `Payments.Approve` | pending payments at the active branch with recorder name and `canApprove` (false for the caller's own) |
| `POST` | `/api/staff/billing/payments/{id}/approve` · `/reject` | `Payments.Approve` | C11 → `200 { applied[], leftUnallocated }` / `204` |
| `POST` | `/api/staff/billing/payments/{id}/allocations` | `Payments.Allocate` | C12 → `200` |
| `POST` | `/api/staff/billing/customers/{customerId}/apply-credit` | `Payments.Allocate` | C13 → `200 { applied[], freeCreditRemaining, invoiceOutstanding }` — **US-BIL-08** |
| `POST` | `/api/staff/billing/payments/{id}/release-earmark` | `Payments.Allocate` | C14 → `204` |
| `POST` | `/api/staff/billing/payments/{id}/reverse` | `Payments.Reverse` | C15 → `204` |
| `POST` | `/api/staff/billing/refunds` | `Refunds.Issue` | C19 → `201 { refundId }` |
| `GET` | `/api/staff/billing/refunds?from&to` | `Billing.View` | paged |
| `GET` | `/api/staff/billing/receivables/ageing?asOf&customerId&pageNumber&pageSize` | `Billing.View` | `{ asOf, totals: { current, days1To30, days31To60, days61To90, over90 }, rows: [{ customerId, customerName, buckets…, oldestDueOn }] }` — **US-BIL-03 AC2** |

**Cash-up and reconciliation (staff)**

| Method | Route | Access | → |
|---|---|---|---|
| `GET` | `/api/staff/billing/cash-ups/{date}` | `CashUp.Count` | `{ status, version, expected: { cash, card, transfer, cashRefunds, cashReversals, carriedIn[] }, lines[] (each payment/refund/reversal), counted?, variance?, explanation?, pendingApprovals, closedBy?, closedOn? }` — **US-BIL-12 AC1** |
| `PUT` | `/api/staff/billing/cash-ups/{date}/count` | `CashUp.Count` | C20 → `200` recomputed view |
| `POST` | `/api/staff/billing/cash-ups/{date}/close` | `CashUp.Close` | C21 → `204`; `400 VarianceExplanationRequired`, `PendingApprovalsExist` |
| `GET` | `/api/staff/billing/cod/collections?courierCode&status=unremitted\|shortfall\|remitted&from&to` | `Reconciliation.Manage` | COD collections with collected, remitted, shortfall, `overdue` (older than `billing.codRemittanceDueDays`) |
| `POST` / `GET` | `/api/staff/billing/cod/remittances` | `Reconciliation.Manage` | C22 → `201` / paged |
| `GET` | `/api/staff/billing/cod/reconciliation?courierCode&from&to` | `Reconciliation.Manage` | per courier: collected, remitted, shortfall total and lines, unapplied remittance amounts — **US-BIL-06 AC1** |
| `POST` | `/api/staff/billing/bank-statements` (multipart: `file`, `bankAccountLabel`, `periodFrom`, `periodTo`) | `Reconciliation.Manage` | parse → C23 → `201 { statementId, lineCount }`; `400 StatementMalformed` with row numbers; `413 StatementTooLarge` |
| `GET` | `/api/staff/billing/bank-statements` · `/{id}/reconciliation` | `Reconciliation.Manage` | list / `{ unmatchedStatementLines[], unmatchedPayments[], matches[] }` — **US-BIL-06 AC2, flagged both ways** |
| `POST` | `/api/staff/billing/bank-statements/{id}/auto-match` | `Reconciliation.Manage` | C24 → `200 { matched, ambiguous, none }` |
| `POST` | `/api/staff/billing/bank-matches` · `/{id}/unmatch` | `Reconciliation.Manage` | C25 → `201` / `204` |
| `GET` | `/api/staff/billing/accounting-exports` | `AccountingExport.Run` | runs: who, when, range, counts, sha256 |
| `GET` | `/api/staff/billing/accounting-exports/download?from&to&branchId` | `AccountingExport.Run` | builds content (query), C26, then streams `application/zip` `purrcare-accounts-{from}-{to}.zip`; `400 ExportRangeTooLarge` |
| `GET` | `/api/staff/billing/customers/{customerId}/loyalty` | `Billing.View` | balance, entries (paged) |

**Customer (own)**

| Method | Route | → |
|---|---|---|
| `GET` | `/api/customers/me/billing/summary` | `{ outstanding, overdue, freeCredit, loyaltyPoints?, loyaltyEnabled }` — HO-POR-001's input |
| `GET` | `/api/customers/me/invoices?pageNumber&pageSize` · `/{id}` · `/{id}.pdf` | issued invoices only; detail includes **how to pay**: the issuing branch's bank accounts and *"use your invoice number as the reference"*; `404` if not the caller's |
| `GET` | `/api/customers/me/payments?pageNumber&pageSize` · `/{id}` | accepted and reversed payments with receipt data |
| `GET` | `/api/customers/me/loyalty` | balance and entries |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8). Amounts in error
messages are formatted in the currency, never raw.

### 12.5 Filters

Extend `UniqueConstraintExceptionFilter`: `ix_charge_source` → `409 ChargeAlreadyRecorded`;
`ix_invoice_line_charge` → `409 ChargeAlreadyInvoiced`; `ix_bank_statement_source` → `400
StatementDuplicate`; `ix_bank_match_line` / `ix_bank_match_payment` → `409 BankLineAlreadyMatched` /
`PaymentAlreadyMatched`; `ix_*_idempotency` → re-dispatch the lookup and return the original result
(the handler's pre-check normally catches it first); `ix_invoice_number` / `ix_invoice_sequence` →
**rethrow** (a numbering bug must be loud, never mapped). Trigger violations (`P0001` raised by the
Billing triggers) are **not** mapped — they indicate a code defect and surface as `500` with the
correlation id (U-X3, I-A3 prove no application path reaches them).

### 12.6 Read side — `src/Read/`

Queries in `Queries/Billing/`, handlers in `QueryHandlers/Billing/`, ViewModels flat; explicit
projections, `AsNoTracking()`.

- `GetCustomerBillingSummaryQuery`, `GetBillingGroupSummaryQuery`, `GetSourceBillingStatusQuery`
  (§8.13) — customer set = the customer plus those merged into it (`account.customer_account.merged_into_customer_id`, ADR-192).
- `GetChargesQuery`, `GetUncapturedSourcesQuery` (appointments `Completed` with no `Appointment`
  charge; Phase J adds the others), `PreviewInvoiceQuery` (calls `InvoiceCalculator` with rates at now).
- `GetInvoicesQuery`, `GetInvoiceDetailQuery` (`allowedActions` from `InvoiceActionRules`, the table
  the aggregate uses), `GetCreditNoteDetailQuery`, `GetInvoiceDocumentQuery` (PDF input).
- `GetPaymentsQuery`, `GetPaymentDetailQuery`, `GetPaymentReceiptQuery`, `GetPendingApprovalsQuery`,
  `GetRefundsQuery`.
- `GetAgeingQuery` — outstanding per issued invoice in SQL; bucketing by `AgeingCalculator` (pure)
  against `asOf` (branch-local today by default).
- `GetCashUpQuery` — rows in SQL, totals by `CashUpCalculator` (the same function the close uses).
- `GetCodCollectionsQuery`, `GetCodReconciliationQuery`, `GetBankReconciliationQuery`.
- `GetAccountingExportContentQuery` — ordered, immutable facts in range; ≤ `MaxExportRows`.
- `GetLoyaltyQuery`; customer-scoped variants of the invoice/payment reads.

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6
— **count them** (I-A2); `IBillableChargeRecorder`, `IDocumentNumbering`, `IBillingPricingInputs`,
the item sources (keyed by `ChargeSourceKind` through `BillableItemSourceResolver`),
`IBankStatementCsvParser`, the renderers and the export writer (Scoped); the three templates via the
catalogue. **No hosted service.** `ValidateScopes`/`ValidateOnBuild` stay on. Request size limit
1 MiB on the statement upload action only.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{BillingRules,ChargeRules,PaymentMethodRules,FinancialYearRules,DocumentNumberFormat,InvoiceStatusRules,InvoiceActionRules,BillingMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Helpers/CardNumberGuard.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs      ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                                ← the nine keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                    ← the three definitions of §8.12 only
src/Domain/CatEcommerce.Domain/Aggregates/Billing/**
src/Domain/CatEcommerce.Domain/Entities/{InvoiceLine,AllocationEntry,RequestedAllocation,CreditNoteLine,RemittanceLine,BankStatementLine,BillingBankAccount}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{SellerSnapshot,BillToSnapshot,PaymentCollector,PaymentEarmark,LoyaltyRedemption,CashUpSnapshot}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{InvoiceCalculator,CashUpCalculator,AgeingCalculator,BankMatchSuggester,IBillableChargeRecorder,IBillableItemSource,IDocumentNumbering,IBillingPricingInputs,IBankStatementCsvParser}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                            ← two methods added only
src/Domain/CatEcommerce.Domain/Models/**                                                            ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Billing/**
src/Application/CatEcommerce.Application/Commands/Billing/**
src/Application/CatEcommerce.Application/CommandHandlers/Billing/**                                 ← incl. BillingPosting
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Billing/**
src/Application/CatEcommerce.Application/DataMappers/BillingMapper.cs
src/Read/CatEcommerce.Read/Queries/Billing/**
src/Read/CatEcommerce.Read/QueryHandlers/Billing/**
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per Billing root}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                            ← the nine migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Billing/**
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs             ← two methods only
src/Api/CatEcommerce.Api/Controllers/{BillingReference,AdminBillingProfiles,StaffBillingCustomers,StaffCharges,StaffInvoices,StaffCreditNotes,StaffPayments,StaffRefunds,StaffReceivables,StaffCashUps,StaffCodReconciliation,StaffBankReconciliation,StaffAccountingExports,CustomerBilling}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Billing/**
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                 ← §12.5 mappings only
src/Api/CatEcommerce.Api/Program.cs                                                                 ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-BIL-001.md
docs/gates/HO-BIL-001-*.md
.ai/decisions/adr.md                                                                                ← ADR-177…194, append only
```

## 14. Forbidden scope

| Path / shape | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-1, OQ-17 are architect tasks) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs; other areas' handovers get Amended rows from the coordinator (§1) |
| `web/**` | HO-BIL-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001, HO-CAT-001 beyond the lines in §13 | Their areas. **No Administration, Appointment or Account aggregate is edited**; Billing reads their tables read-only through its own Infrastructure inputs |
| **Payment gateway anything** — SDK, provider adapter, webhook controller, online `PaymentMethod` member, `payment_intent` table, token column, card-number column, settlement-file import, subscription auto-charge | SRS Phase 5; backlog §18.5; owner instruction; C-5 |
| Order, cart, boarding, visit, prescription, surgery aggregates or tables | HO-ORD / HO-BRD / HO-EMR / HO-PHM / HO-SRG |
| A background worker, a second host, a queue, a second `DbContext` type | ADR-010; nothing in Billing is timed |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — calculator and tax (US-BIL-07, ADR-181)** | | |
| U-A1 | Line 2 × 450.00, discount 50.00, rate 15 % | net 850.00, tax 127.50, gross 977.50 |
| U-A2 | Net 33.33 at 7.5 % | tax 2.50 (2.49975 → half away from zero) ; 0.005 cases round up in magnitude |
| U-A3 | Three lines with different classes (15 %, 5 %, 0 %) | tax per line; invoice tax = Σ line tax, never tax of the sum |
| U-A4 | Loyalty value 100.00 over qualifying nets 300.00 / 200.00 / 100.00, fee line excluded | shares 50.00 / 33.33 / 16.67 (largest remainder to the largest line); fee untouched; tax on the reduced nets |
| U-A5 | Discount greater than quantity × price | `DiscountExceedsAmount` |
| U-A6 | Mixed currencies | `CurrencyMismatch` |
| U-A7 | Reflection over `InvoiceCalculator`, `CashUpCalculator`, the aggregates | no `double`/`float` member or local |
| U-A8 | Credit of 1 of 3 units at the frozen 15 % | net, tax and gross proportional; rate taken from the line, not from settings |
| **B — charges and capture (ADR-178)** | | |
| U-B1 | `Record` valid; quantity 0; negative price; description blank | valid / `InvalidQuantity` / refused / refused |
| U-B2 | Default business line per source kind | Appendix of §8.2 |
| U-B3 | Recorder with the source key already tracked in the context; already in the table | returns the existing id; nothing added |
| U-B4 | Appointment item source: completed appointment with snapshot 800.00 and service tax class T | one Service line, `Clinic`, `ServiceDate` = local completion date, tax class T |
| U-B5 | Capture handler whose command throws | exception swallowed, `Error` logged with ids only, nothing rethrown |
| U-B6 | Capture run twice | second run records 0 |
| **C — invoices and numbering (US-BIL-01, 02, 05)** | | |
| U-C1 | `FinancialYearRules.For` 2026-06-30 / 2026-07-01 with start month 7; start month 1 | `2025-26` / `2026-27`; `2026` |
| U-C2 | `DocumentNumberFormat` | `INV-MAIN-2627-000123`, `CN-MAIN-2627-000007` |
| U-C3 | Series `Take` ×3 | 1, 2, 3; `NextSequence` 4 |
| U-C4 | `InvoiceStatusRules.Derive` full table (paid 0/part/full × credited 0/part/full) | as §8.6; outstanding never negative |
| U-C5 | Every non-draft status × every mutating method except `ApplyPayment`/`ReleasePayment`/`ApplyCredit` | `InvoiceNotDraft` — **US-BIL-05 AC1** |
| U-C6 | Issue with a line whose class has no rate at now | `TaxRateNotConfigured` naming the class |
| U-C7 | Issue without a billing profile | `BillingProfileMissing` |
| U-C8 | 501 lines | `InvoiceTooManyLines` |
| U-C9 | FY start month changed so today falls in an earlier FY than the branch's latest series | `FinancialYearConfigurationChanged` |
| **D — payments (US-BIL-11, 03, 04; ADR-182, 183)** | | |
| U-D1 | Every `PaymentMethod` member | every flag defined; `IsOnline == false` for all; `AllowedRefundMethods` non-empty |
| U-D2 | Card-in-store without reference; COD without collector; transfer without reference | `ReferenceRequired` / `CollectorRequired` / `ReferenceRequired` |
| U-D3 | `CardNumberGuard`: `4111 1111 1111 1111`, `4111-1111-1111-1112` (Luhn fails), slip `004512`, a 20-digit number | true / false / false / false |
| U-D4 | Amount at threshold; one paisa above | `Accepted` / `PendingApproval` with requested allocations, nothing applied |
| U-D5 | Approve by the recorder | `ApproverMustDiffer` — **US-BIL-11 AC2** |
| U-D6 | Approve when the invoice's outstanding fell below the requested allocation | applied capped; remainder unallocated; reported |
| U-D7 | `Unallocated` after allocate 600 of 1000, release 100, refund 200 | 300 |
| U-D8 | Allocate more than unallocated; more than outstanding | `AllocationExceedsUnallocated` / `AllocationExceedsOutstanding` |
| U-D9 | Earmarked (BoardingStay S) allocation to an invoice without S lines; after `ReleaseEarmark` | `EarmarkMismatch` / allowed |
| U-D10 | Reverse with refunds; reverse without | `PaymentHasRefunds` / every allocation released, status `Reversed` |
| U-D11 | Reflection: public setters on amount, method, reference, received date, collector | none — **US-BIL-11 AC3** |
| U-D12 | `ReceivedOn` tomorrow in branch time; 32 days ago | `ReceivedDateInFuture` / `ReceivedDateTooOld` |
| **E — credit notes and refunds (US-BIL-05 AC2)** | | |
| U-E1 | Credit exceeding a line's remaining; exceeding the invoice | `CreditExceedsLine` / `CreditExceedsInvoice` |
| U-E2 | Fully paid 1000 invoice (two payments, 600 then 400), credit 500 | 400 released from the newest payment, 100 from the older; `ReleasedToCustomerCredit` 500 |
| U-E3 | Unpaid invoice, credit 300 | nothing released; outstanding reduced |
| U-E4 | Refund method not allowed for the original (transfer paid, cash refund) | `RefundMethodNotAllowed` |
| U-E5 | Refund above unallocated | `RefundExceedsAvailable` naming the available amount |
| **F — cash-up (US-BIL-12, BIL-06 AC3)** | | |
| U-F1 | Calculator: cash 3 × 500, staff COD 200, courier COD 900, card 700, transfer 1000, cash refund 100, cash reversal 50, carried-in 20 | expected cash 1570; card 700; transfer 1000; courier COD excluded |
| U-F2 | Close with variance −10 and no explanation; with explanation | `VarianceExplanationRequired` / closed — **US-BIL-12 AC2** |
| U-F3 | Close with a pending approval that day | `PendingApprovalsExist` |
| U-F4 | Count a future date; recount a closed day | `CashUpDateInFuture` / `CashUpClosed` |
| **G — reconciliation (US-BIL-06)** | | |
| U-G1 | Suggester: one candidate equal amount, date within 3 days, reference token match | matched |
| U-G2 | Two equal candidates; amount differs by 0.01; date 4 days off | none matched (ambiguous / none / none) |
| U-G3 | Reference normalisation (case, spaces, dashes) | `TRX-00 12` matches `trx0012` |
| U-G4 | CSV parser: valid; missing header; bad date on row 7; amount `1,500.00`; 1 MiB + 1 byte | lines / `StatementMalformed` / names row 7 / refused / `StatementTooLarge` |
| U-G5 | Statement description containing a Luhn-valid 16-digit run | stored masked `••••1111` |
| U-G6 | COD shortfall: collected 1200, remitted 1000 then 150 | shortfall 50 |
| **H — export (US-BIL-09, ADR-190)** | | |
| U-H1 | Writer with the same facts twice | byte-identical output and SHA-256 |
| U-H2 | Zip entries | fixed order and `LastWriteTime` (F36) |
| U-H3 | Cell beginning `=`, `+`, `-`, `@` | prefixed `'` |
| U-H4 | Rows | every row has `recordType` and `documentId`; no current-status column |
| **I — loyalty and credit (US-BIL-08, 10)** | | |
| U-I1 | Accrual on qualifying net 1234.00 at 100 per point | 12 points; fee lines excluded |
| U-I2 | Handler twice for the same invoice | one entry (unique key) |
| U-I3 | Redemption below minimum; above balance; value above qualifying net | `LoyaltyBelowMinimum` / `LoyaltyInsufficientPoints` / `LoyaltyRedemptionExceedsNet` |
| U-I4 | Reversal larger than balance | balance 0, `Shortfall` recorded |
| U-I5 | Disabled | accrual no-op; redemption `LoyaltyNotEnabled` |
| U-I6 | `ApplyCustomerCredit` across two free-credit payments and one earmarked | oldest free first; earmarked untouched |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Billing/**` | no `string` properties |
| U-X2 | `ActionRoles` | every `Billing.*`, `Payments.*`, `CreditNotes.*`, `Refunds.*`, `CashUp.*`, `Reconciliation.*`, `AccountingExport.*`, `BillingProfile.*` action mapped; no `Customer` in a staff list |
| U-X3 | Migration trigger and predicate SQL | uses `InvoiceStatus.X.Value`; every Billing table except draft lines refuses `DELETE` |
| U-X4 | Scan Application, Read, Api for `PaymentMethod.<Member>` outside `PaymentMethodRules` | none — ADR-182 |
| U-X5 | The three template definitions | pass HO-NTF-001 `TemplateRules`; all `Transactional` |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Staff signed in through the
real cookie path. HO-NTF-001's `NotificationWorkCycle` is called directly where messages are asserted.

### 16.1 Additional shared-database rules

1. **Each test creates its own branch** (HO-ADM-001 endpoints) with its own billing profile, tax class
   and rates — so its invoice series, cash-ups and statements are its own.
2. **Invoice numbers are never asserted as literals** — only as `INV-{thatBranchCode}-…` and contiguous
   within the test's series.
3. Producers that do not exist yet (Shop, Boarding, Pharmacy) are simulated by **resolving
   `IBillableChargeRecorder` in a DI scope inside the test** and saving — the same port HO-ORD-001 and
   HO-BRD-001 will call. No test-only endpoint is added to the Api.
4. Triggers are real; a test never disables them.

| ID | Case | Expected |
|---|---|---|
| **A — foundation** | | |
| I-A1 | Migrations on an empty database | `billing` schema, every table, index, trigger and `receipt_number_seq` |
| I-A2 | Host starts with scope validation | every registration resolves |
| I-A3 | Through Npgsql: `UPDATE` an issued invoice's total; `UPDATE`/`DELETE` an issued line; `DELETE` a payment; `UPDATE` a payment's amount; `DELETE` a credit note | each raises — **US-BIL-05 AC1**, D-5 |
| I-A4 | Set the billing profile; read it | round-trip; `DataChange` audit row |
| **B — charges** | | |
| I-B1 | Complete an appointment (HO-APT-001 endpoints) | one charge, price = the appointment's snapshot even after the service price changed — ADR-049 |
| I-B2 | Break capture (tax class removed from the service in the test's data), complete an appointment | the appointment's request still `204`; the appointment appears in `/uncaptured-sources`; after fixing, `POST …/capture` records the charge |
| I-B3 | Manual charge for a service; for a free-text fee | service price at now; fee as entered |
| I-B4 | Void an unbilled charge; void a billed one | `204` / `400 ChargeAlreadyInvoiced` |
| I-B5 | Recorder via DI scope: same source key twice in two requests concurrently | one charge; the other `409 ChargeAlreadyRecorded` or deduplicated by pre-check — never two rows |
| **C — invoices** | | |
| I-C1 | Draft from a consultation charge, a shop product charge and three boarding nights (recorder) for one customer; issue | lines itemised with kind, business line, tax per line; totals as the calculator — **US-BIL-02, AC-16, US-ORD-17 server half** |
| I-C2 | Issue five invoices sequentially | contiguous sequence in the test's series — **US-BIL-01 AC1** |
| I-C3 | **Issue 20 invoices concurrently** in one branch | 20 distinct, contiguous numbers; no gap; no duplicate; retries observed in logs — **US-BIL-01 AC2**, F35 |
| I-C4 | Issue, then change the tax rate (new effective rate), issue another | the first keeps its rate and totals; the second uses the new rate — **US-BIL-07 AC2** |
| I-C5 | Two drafts claiming the same charge concurrently | one `201`, one `409 ChargeAlreadyInvoiced` |
| I-C6 | Discard a draft | its charges unbilled again; a new draft can take them |
| I-C7 | `issue-from-charges` with `Source(Order, id)`; with `BillingGroup(stay, id, ServiceDateTo)` | only that source's / that group's charges up to the date — BRD-20 support |
| I-C8 | Issue with the same `Idempotency-Key` twice | one invoice; second `200` with the same number |
| I-C9 | PDF of an issued invoice | `application/pdf`; contains the number, the seller's tax registration number and each line — NFR-LEG-3 |
| I-C10 | Issue → the customer's inbox | `billing.invoice-issued` in-app copy after the delivery pass |
| **D — payments** | | |
| I-D1 | Record 400 cash against a 1000 invoice | `Accepted`; receipt number; invoice `PartPaid`, outstanding 600 — **US-BIL-03 AC1** |
| I-D2 | Then 350 card (slip ref) and 250 transfer (ref) | invoice `Paid` after the third — **US-BIL-04** |
| I-D3 | Record above the threshold | `PendingApproval`; invoice unchanged; `/approvals` lists it; the recorder's `approve` → `400 ApproverMustDiffer`; the accountant approves → applied — **US-BIL-11 AC2** |
| I-D4 | Reception calls `approve` | `403` |
| I-D5 | Double-submit a payment with one key (parallel) | one payment; both responses carry its id |
| I-D6 | Reference `4111 1111 1111 1111` | `400 ReferenceLooksLikeCardNumber`; the digits appear in no log line and no audit row — C-5 |
| I-D7 | Reverse a payment | `Reversed`; invoice back to `PartPaid`; `BusinessEvent` audit with the reason; re-entry works — **US-BIL-11 AC3** |
| I-D8 | `PUT`/`PATCH` on a payment | no such route (`405`) |
| I-D9 | Deposit earmarked to a stay, then a stay invoice issued, then allocate | allocated; earmark mismatch refused for another invoice |
| I-D10 | Summary and ageing with invoices due 10, 40, 75 and 100 days ago | buckets correct; summary outstanding and free credit — **US-BIL-03 AC1, AC2** |
| I-D11 | Every record/approve/reverse | audited with actor, IP, before/after — BR-17, C-9 |
| **E — credit notes and refunds** | | |
| I-E1 | Credit a paid invoice partially | CN number in the CN series; invoice outstanding recomputed; excess released to credit; summary `freeCredit` increased |
| I-E2 | Corrected draft from the CN; edit a line; issue | new invoice with `correctsInvoiceId`; original shows `correctedBy` — **US-BIL-05 AC2** |
| I-E3 | Refund 200 cash from the released credit | refund recorded; `freeCredit` reduced; `billing.refund-issued` enqueued |
| I-E4 | Refund a deposit directly (no CN) with reason | allowed; audit row with reason |
| **F — cash-up** | | |
| I-F1 | Day with cash, card, transfer, COD (staff and courier), a refund | expected as U-F1 — **US-BIL-12 AC1** |
| I-F2 | Count with a variance; close without explanation; with | `400` / `204`; `BusinessEvent` with the explanation — **US-BIL-12 AC2, US-BIL-06 AC3** |
| I-F3 | A payment accepted after close for the same date | appears as carried-in on the next day's cash-up |
| I-F4 | Reception closes | `403` (OQ-12) |
| **G — reconciliation** | | |
| I-G1 | Three courier COD collections 500/700/300; remittance 1400 covering the first two with 500 and 600 | shortfall 100 on the second; the third listed unremitted — **US-BIL-06 AC1, AC-8 (reconciliation half)** |
| I-G2 | Import a statement (5 credits) against 4 recorded transfers; auto-match | 3 matched; 1 statement line unmatched; 1 payment unmatched; ambiguous pair left for manual — **US-BIL-06 AC2** |
| I-G3 | Import the same file again | `400 StatementDuplicate` |
| I-G4 | Manual match with different amounts; with a cash payment | `400 MatchAmountMismatch` / `MatchMethodNotTransfer` |
| I-G5 | Concurrent auto-match twice | no duplicate matches |
| **H — export** | | |
| I-H1 | Download for a range twice (with a payment and a credit note in between for **another** date) | byte-identical ZIPs; two run records — **US-BIL-09 AC2** |
| I-H2 | Contents | invoices, invoice-lines, payments, allocations, credit-notes, refunds and manifest; `documentId` on every row — **US-BIL-09 AC1** |
| I-H3 | Range 400 days | `400 ExportRangeTooLarge` |
| **I — loyalty and credit** | | |
| I-I1 | Loyalty enabled; pay an invoice in full | points accrued once — **US-BIL-10 AC1** |
| I-I2 | Redeem 200 points on a draft; issue | discount row on the invoice and PDF; tax on reduced nets; balance reduced — **US-BIL-10 AC2** |
| I-I3 | Apply customer credit partially then fully | outstanding and `freeCreditRemaining` after each — **US-BIL-08** |
| **J — access and merge** | | |
| I-J1 | Customer reads own invoice, PDF, payments; another customer's | `200` / `404` each |
| I-J2 | Customer lists invoices | drafts and pending payments absent |
| I-J3 | Each Billing endpoint × each role (full matrix) | per §12.3 |
| I-J4 | Merge a duplicate with an unbilled charge, a draft, an issued invoice and a payment | charge and draft moved; invoice and payment keep the old id; survivor's summary includes all |
| **X** | | |
| I-X1 | Log capture across all cases | no reference that looks like a card number, no statement description, no reason text at `Information`+ |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers |
|---|---|---|
| **A** | — | Enumerations, constants, codes, settings keys, `CardNumberGuard`, `PaymentMethodRules`, `InvoiceCalculator`, `FinancialYearRules`, billing profile, number series, `DocumentNumbering`, `BillingPosting`; migration `AddBillingFoundation` |
| **B** | BIL-02 (charges) | `ChargeAggregate`, recorder, manual charges, void, appointment capture, uncaptured list; `AddBillingCharges` |
| **C** | BIL-01, 02, 05 AC1, 07 | Invoices, drafts, issue (both forms), numbering retry, triggers, PDF, invoice message; `AddBillingInvoices` |
| **D** | BIL-11, 03, 04 | Payments, approval, allocation, earmarks, reversal, receipts, summary, ageing, receipt message; `AddBillingPayments` |
| **E** | BIL-05 AC2, FR-BIL-5 | Credit notes, corrected re-issue, refunds; `AddBillingCreditNotesAndRefunds` |
| **F** | BIL-12, BIL-06 AC3 | Cash-up; `AddBillingCashUp` |
| **G** | BIL-06 AC1, AC2 | COD remittances, bank statements, matching; `AddBillingReconciliation` |
| **H** | BIL-09 | Export; `AddBillingAccountingExport` |
| **I** | BIL-08, BIL-10 | Customer-credit tender, loyalty; `AddBillingLoyalty` |
| **J** | contract | Visit, dispense and surgery item sources and handlers — **only after HO-EMR-001, HO-PHM-001, HO-SRG-001 are merged** and their real events compared with §8.13 |

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
#   READ the migration — triggers and the receipt sequence are hand-written SQL
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | BIL-01 AC1 | Each issued invoice takes the next number for its branch and financial year, no gaps, no reuse | U-C1…U-C3, I-C2 |
| A2 | BIL-01 AC2 | Concurrent issuance never duplicates a number | **I-C3**, I-C8 |
| A3 | BIL-02 AC | Services, products, medicines and boarding nights are itemised with tax per line | U-A1, U-A3, I-C1, I-C9 |
| A4 | ORD-17 / AC-16 *(server half)* | One invoice combines clinic, shop and boarding charges, each line attributed to its business line | U-B2, I-C1 |
| A5 | BIL-02 | Charges reach Billing from appointments without loss; failures are listed and recoverable | U-B4…U-B6, I-B1, I-B2, I-B5 |
| A6 | BIL-03 AC1 | Partial payments and deposits recorded; outstanding balance visible for the customer | U-D7, U-D9, I-D1, I-D9, I-D10 |
| A7 | BIL-03 AC2 | Ageing groups outstanding balances by days overdue | I-D10 |
| A8 | BIL-04 AC | Several payments of different methods apply until the balance clears | I-D2 |
| A9 | BIL-05 AC1 | An issued invoice cannot be edited — application and database | U-C5, I-A3 |
| A10 | BIL-05 AC2 | A correction is a credit note plus a linked, re-issued corrected invoice | U-E1…U-E3, I-E1, I-E2 |
| A11 | BIL-06 AC1 | COD remittances reconcile against collected COD and list shortfalls | U-G6, I-G1 |
| A12 | BIL-06 AC2 | Transfers match the bank statement; unmatched entries flagged both ways | U-G1…U-G5, I-G2…I-G5 |
| A13 | BIL-06 AC3 | The daily till count reconciles against in-store cash recorded that day | U-F1, I-F1, I-F2 |
| A14 | BIL-11 AC1 | Method, amount, currency, reference, date received and recorder are recorded | U-D2, U-D12, I-D1, I-D11 |
| A15 | BIL-11 AC2 | Above the threshold a second, different staff member must approve | U-D4…U-D6, I-D3, I-D4 |
| A16 | BIL-11 AC3 | Payments are audited and never edited; corrections by reversal and re-entry | U-D10, U-D11, I-D7, I-D8, I-D11 |
| A17 | BIL-12 AC1 | Cash, card and transfer payments of the day listed with expected totals | U-F1, I-F1 |
| A18 | BIL-12 AC2 | Counted cash gives a variance; an explanation is required before close | U-F2, U-F3, I-F2 |
| A19 | BIL-07 AC1 | Tax per line at the tax class's configured rate | U-A1…U-A3, U-C6, I-C1 |
| A20 | BIL-07 AC2 | Invoices issued before a rate change keep their rate | U-A8, I-C4 |
| A21 | BIL-08 AC | Customer credit applies as tender, partially or fully, remaining balance shown | U-I6, I-I3 |
| A22 | BIL-09 AC1 | Invoices, payments and credit notes export for a date range | U-H4, I-H2 |
| A23 | BIL-09 AC2 | The same range exports identically; duplicates identifiable by `documentId` | U-H1, U-H2, I-H1 |
| A24 | BIL-10 AC1 | Points accrue on qualifying spend across all three lines | U-I1, U-I2, I-I1 |
| A25 | BIL-10 AC2 | Points redeem as a discount shown on the invoice | U-A4, U-I3, I-I2 |
| A26 | C-5 / ORD-07 AC3 | No card number is stored anywhere; card-like references refused | U-D3, U-G5, I-D6, I-X1 |
| A27 | FR-BIL-5 / ORD-15 *(server half)* | Refunds record method, amount, reference, staff and reason, linked to the invoice via a credit note | U-E4, U-E5, I-E3, I-E4 |
| A28 | ADR-182 | No online method, token or gateway code exists; behaviour reads flags only | U-D1, U-X4 |
| A29 | — | Every endpoint's access matches §12.3; customers see only their own issued documents | U-X2, I-J1…I-J3 |

**Deferred, named in §4.2**: order-side COD/transfer/refund flows (HO-ORD-001); deposit confirmation,
cycle invoicing trigger and spending limit (HO-BRD-001); visit/dispense/surgery items (Phase J after
HO-EMR/PHM/SRG); portal dashboard (HO-POR-001); reports (HO-RPT-001). **Not built**: gift vouchers
(OQ-7), scheduled export (OQ-19), gateway (Phase 5).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Billing` context (ADR-177, OQ-1); push/pull charge paths and the fresh capture scope (ADR-178); the posting exception and follow-up event dispatch (ADR-184, F31); numbering without sequences or locks (ADR-179); **no enqueue from any event handler**; no gateway shape; edits to HO-ADM/NTF/ACC files confined to §13's lines; no other area's aggregate edited |
| 2 | SECURITY | `security-reviewer` | **C-5**: card-number guard on every free-text money field and no PAN in logs (I-D6, I-X1); approval separation (recorder ≠ approver, I-D3); immutability triggers (I-A3); `404` ownership on customer routes; idempotency against double payment (I-D5); CSV upload bounds and in-memory parsing; export injection guard and audit; `Payments.Approve`/`Reverse` restricted |
| 3 | CODE_REVIEW | `code-reviewer` | All money through `InvoiceCalculator` with explicit rounding; no `double`; `PaymentMethod` identity only in `PaymentMethodRules`; handlers follow §11.4's posting/numbering/idempotency sequence; explicit projections |
| 4 | TEST | `test-engineer` | `TC-HO-BIL-001.md` from §15/§16 first; §16.1 rules; **I-C3 and I-D5 with real parallelism**; triggers exercised through Npgsql, not only through EF |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A4 and A27 marked server-half; §4 deferrals and the open questions' defaults recorded, nothing gateway-related built |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001, HO-ADM-001, HO-NTF-001 or HO-APT-001 is not merged.** Billing extends their
filter, role table, settings registry, template catalogue and link builder, reads their tax rates and
consumes the appointment event. Do not stub them.

**20.2 Phase J: the real HO-EMR-001 / HO-PHM-001 / HO-SRG-001 events or billable data differ from
§8.13.** Stop and report the difference; the fix is an Amended row here, not an adapter that guesses.
Never build Phase J against assumed tables.

**20.3 Do not build any part of the payment gateway** (§4.3, §14) — no online member "for later", no
token column, no webhook. The seam is ADR-182's flags and nothing more.

**20.4 A gap appears in an invoice or credit-note series** in any test or environment. Stop: numbering
is wrong. Never "fix" it with a sequence, a lock or by accepting gaps.

**20.5 Do not edit an issued document or a payment's core fields** — not through EF, not by raw SQL,
not by disabling a trigger in a migration or a test.

**20.6 A card number is found anywhere** — a column, a log, an audit row, an export. Security-gate
BLOCKER; stop.

**20.7 Do not enqueue from an event handler** — HO-NTF-001 ADR-057.

**20.8 Do not add a background job** — nothing here is timed; bank-transfer and deposit expiry belong
to HO-ORD-001 and HO-BRD-001.

**20.9 QuestPDF licence** (HO-CAT-001 §20.6) — if the Community licence does not apply, stop; do not
swap in an AGPL library.

**20.10 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope. In
particular, do not resolve OQ-17 (money representation) by changing HO-ADM-001's `Money`.

**20.11 An open question's default proves wrong in use** — an Amended row changes it, never a silent
code change.
