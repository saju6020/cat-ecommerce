GATE: ARCHITECTURE
HANDOVER: HO-BIL-001
COMMITS: 0c1cff7 39ea24b 8f260e7 d5ca029 4c0327a 712fe0a 2be7855 260b54d bd47c03 b511d66 d5115e8 418ff81 92cc46b 3ac2e82 ffb70cc (ho/BIL-001, `git diff 50f1ab1..ffb70cc`; 84a222b and 1d246fe are the parallel CODE_REVIEW/SECURITY gate files and change no code)
DATE: 2026-10-07
STATUS: FAIL

What this gate did:
- Read HO-BIL-001 in full and CONTRACTS.md (the C-ALL duties).
- Read every `.ai/` doc that §6 names, plus the mandatory set. §6 is complete.
- Reviewed the 262 changed files. It did not build, run tests or touch a database. Anything that depends on runtime behaviour is marked for TEST.
- Phase J (the visit, dispense and surgery item sources) is DEFERRED by owner decision and is not failed for being absent.

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings go to docs/backlog/BIL.md.

CLEAN — checked and compliant
- **Layers.**
  - Domain references only Shared and Platform.Core: no EF, Application, Read or Api type.
  - Application does not reference Read or Infrastructure. Read does not reference Application.
- **Forbidden shapes.** None of these appears anywhere in the diff:
  - IBusinessMessageDispatcher, IDispatcher, IEventPublisher, MassTransit, RabbitMQ, Mongo or Bson;
  - QueuePublishOnly, AddServiceBusProvider, 202, a rejection event, CommandServices, IReadRepository or ViewModelBase;
  - a hosted service, BeginTransaction, FOR UPDATE, an advisory lock, a sequence for invoice or credit-note numbers, float or double, or [Authorize].
- **Controllers.** They inject only IInMemoryDispatcher, accessors, renderers and the CSV parser that §11.1 C23 sanctions. None holds a DbContext, a repository or an aggregate.
- **Command stamping.** Every SendAsync stamps `ValidateCommand = true`.
- **Registration.** BillingModule registers every handler, validator (as IValidator<T>), query handler and event handler explicitly, and all Scoped. Nothing from AddInMemoryBusServices is hand-registered. Program.cs is untouched (ADR-341).
- **Persistence.**
  - 14 roots, each with an `xmin` row version.
  - Every aggregate child is mapped with OwnsMany. There is no HasMany.
  - Money is `numeric(12,2)` plus a code (ADR-194). Instants are `timestamptz`.
  - Enumerations are converted on `Value`.
- **Aggregates.** No public setters. Every mutation calls UpdateVersion(). Other aggregates are referenced by id.
- **ADR-184 postings.** They go through BillingPosting, with follow-up dispatch on the secondary roots, except as noted in HIGH 2.
- **Event handlers.** None enqueues. Capture uses a fresh scope and swallows failures (ADR-178).
- **ADRs.** ADR-177…194 in `.ai/decisions/adr.md` match §9 word for word. A reconstructed `| **ADR-n** | Decision | Reason |` row was matched exactly against §9 for all 18. The change is append-only, and ADR-195/196 are left free.
- **C-ALL duties.**
  - C-ALL-1: all 21 billing tables are classified in RecordClassification.Billing.cs.
  - C-ALL-2: BillingPersonalDataEraser is registered. It reports Held under D-9 and mutates nothing.
  - C-ALL-3: BillingRetentionReviewSource is registered.
  - C-ALL-4: no aggregate method removes a record from a Forbidden table. Only draft invoice lines, the requested-allocation working set and the profile's bank accounts are replaced wholesale, and those are AggregateOnly or still drafts.
  - C-ALL-5: not applicable; Billing is not clinical.
- **§8.13 contracts.**
  - IBillableChargeRecorder is unchanged.
  - ChargeRequestDto and ChargeSourceKey are as published.
  - IssueInvoiceFromChargesCommand, RecordPaymentCommand, PaymentAcceptedEvent, InvoiceIssuedEvent, InvoiceStatusChangedEvent, CreditNoteIssuedEvent, PaymentReversedEvent and RefundIssuedEvent match their published shapes.
  - The three published queries exist.
- **Phase J.** Nothing is built or stubbed: no Visit, Dispense or Surgery source and no assumed event. IBillableItemSource and BillableItemSourceResolver leave the seam open. An unsupported kind is refused with `SourceKindNotCapturable`.
- **Cross-area edits** stay within §13's lines: the link builder, the template catalogue, the filter's §12.5 mappings, and two test fakes that gain the two new link methods.
- **Test infrastructure.** BillingApiFactory and BillingLoggedApiFactory are new files under `tests/CatEcommerce.IntegrationTests/**` and change no production code.

DEVIATIONS 1–18 (developer-declared)
1. **IBillingFinder / EfBillingFinder: compliant.** It is a Domain port of AsNoTracking id and value lookups, on the IFlaggableAppointmentFinder precedent. Handlers still load the aggregates they mutate through IAggregateRootRepository. Outside §13's literal list, so MEDIUM 9.
2. **Receipt number taken before the save via nextval: compliant.** Gaps are allowed for receipts (§8.8), and ADR-179 covers document numbers only. The column keeps its DB default. LOW.
3. **Numbering retry on unique violations, plus an in-process gate: compliant, and safe across instances.**
   - Correctness rests on `xmin` and the unique number indexes, which hold across processes.
   - A unique violation on ix_invoice_number/sequence or ix_credit_note_number/sequence is the same stale-series race, seen when EF sends the document INSERT before the series UPDATE.
   - The retry reloads only the series row, as ADR-179 says. A genuine numbering bug reloads the same number, uses up its 5 attempts and rethrows, so it stays loud.
   - The static SemaphoreSlim serialises only within one process. It reduces collisions and is not a PostgreSQL lock, so ADR-179's "no lock" holds.
   - With N instances, at most N issuers per series contend. Five attempts can run out under heavy cross-instance contention, which gives a failed request but never a gap or a duplicate.
   - TEST: I-C3 is single-process, so it never exercises two processes committing in parallel (see NOTES).
4. **BillingIdempotencyReplayFilter: functionally per §12.5, but placed outside §13 and it bypasses dispatch.** MEDIUM 4.
5. **Manual charge built by its own handler rather than the recorder: compliant.** It is the canonical `.ai/command-handler-pattern.md` shape (create the aggregate, the aggregate enforces, the repository saves). The recorder's pre-check is meaningless for a freshly minted `SourceId = ChargeId`. LOW: it deviates from the text of §11.4 C2.
6. **loyalty_entry unique indexes filtered on `idempotent_source`: compliant.** The literal §12.1 index on `(kind, invoice_id)` would block redeem, remove, then redeem again on the same draft. Not recorded in §1 Amended. LOW.
7. **Merge transfer entries carry the duplicate's id as SourceCustomerId: compliant.** It gives one Out and one In entry per duplicate, idempotent by `(kind, source_customer_id)`. The posting that writes them is HIGH 2.
8. **Extra ports and services.**
   - CodShortfall and LoyaltyCalculator are pure. LoyaltyDependencies is a parameter record, not a service.
   - ILoyaltyAccountStore uses the same INSERT … ON CONFLICT shape as §8.7's EnsureSeries.
   - IBankMatchRecorder is justified (only the data layer can drop a failed insert), but it silently drops domain events (MEDIUM 5).
   - None is a repository-wrapping service layer (ADR-015). Scope is MEDIUM 9.
9. **Credit-note excess release posted in the same save: compliant** with §8.9 step 4 and ADR-184.
10. **PaymentActionRules inside InvoiceActionRules.cs: compliant.** The file is in §13. LOW (discoverability).
11. **Extra no-delete triggers: compliant.** §12.1 says "No Billing table accepts DELETE" (D-6).
12. **Deterministic cash-up id: compliant.** The unique index stays. LOW: a concurrent first count on the same day surfaces as an unmapped PK violation, because `cash_up` is not in BillingFirstWriteTables.
13. **60-day window for late items: MEDIUM 6.** It bounds ADR-188's "never lost".
14. **Accounting export built in memory with the MaxExportRows cap: MEDIUM 7.**
15. **CSV row counting: compliant, LOW.** It is a CODE_REVIEW concern.
16. **Extra violation code StatementNotFound: compliant.** It is append-only. LOW. Also undeclared: SourceKindNotCapturable, SourceNotFound, BankMatchNotFound, ServiceNotPriced.
17. **Per-area partials: compliant.** They are ADR-341's approved pattern: BillingModule, CatEcommerceDbContext.Billing, and Actions, ActionRoles, BusinessViolationCodes, SettingDefinitions and SettingKeys `.Billing`.
18. **No `.ai/flows` updates: correct.** §14 forbids `.ai/**` except decisions/adr.md.

FINDINGS
- [HIGH] The read side rehydrates aggregates. This breaks `.ai/read-side-pattern.md` §4 rule 1 ("project, never rehydrate") and ADR-012. It is the same failure that CAT, NTF, SHP and EMR were failed for.
  - **Each site below materialises a whole aggregate with `AsNoTracking()` and then maps it in memory**, instead of a `Select` projection. Owned collections load automatically, so this pulls entire graphs: up to 500 invoice lines, 200 allocation entries, 5,000 statement lines.
  - **EF Core's `ToDictionaryAsync(key, value)` does not project in SQL.** It materialises the entity first, then applies the selectors on the client.
  - **The sites:**
    - `src/Read/CatEcommerce.Read/QueryHandlers/Billing/InvoiceQueryHandlers.cs`:
      - :67 GetInvoiceDetail;
      - :221 GetInvoiceDocument;
      - :286-287 GetCustomerInvoiceDetail.
      - InvoiceDetailBuilder and InvoiceViewMapper take an `InvoiceAggregate`.
    - `PaymentQueryHandlers.cs`:
      - :24 `StaffAccountAggregate` via ToDictionaryAsync (HO-ACC-001's aggregate);
      - :32 `InvoiceAggregate` via ToDictionaryAsync, which loads every line of every invoice just to read its number;
      - :89 GetPaymentDetail;
      - :145 GetPaymentReceipt;
      - :149 `BillingProfileAggregate`;
      - :173-177 GetPendingApprovals (a list of whole PaymentAggregates);
      - :180 `CustomerAccountAggregate` via ToDictionaryAsync;
      - :197 GetPaymentRecordResult (also used by the controller and the replay filter).
    - `CreditNoteQueryHandlers.cs`: :48, :78 and :79 (a credit note plus the whole invoice it credits, only for the two snapshots).
    - `CashUpQueryHandlers.cs`: :26.
    - `ReceivablesQueryHandlers.cs`: :119 (`CustomerAccountAggregate` via ToDictionaryAsync).
    - `ReconciliationQueryHandlers.cs`: :213 (a BankStatementAggregate with up to 5,000 owned lines) and :220 (a list of BankMatchAggregates).
  - **Contrast.** GetInvoices, GetPayments, GetCharges, the summary, ageing and group reads, the export content and GetBillingProfile all project correctly, so the fix pattern is already in the branch.
  - **No exception like SHP's ADR-095 applies.** `InvoiceActionRules.AllowedActions(status, outstanding)` and `PaymentActionRules.AllowedActions(...)` take primitives, not the aggregate.
  - **Fix.** Project into anonymous or row types (`SelectMany` over the owned lines and entries), and select `{ Id, FullName }` / `{ Id, Number }` before building dictionaries.
- [HIGH] `src/Application/CatEcommerce.Application/DomainEventHandlers/Billing/ReassignBillingOnCustomerMergedHandler.cs:72-73` — the loyalty merge transfer is committed in **two** saves. That breaks ADR-184, which says loyalty operations commit their roots "in **one** `SaveChangesAsync` (atomic)".
  - **What the code does.**
    - :72 commits the `MergeTransferOut` entry together with both accounts: the duplicate drops to 0 and the survivor gains the points.
    - :73 then saves the `MergeTransferIn` entry separately.
  - **The failure mode.** If :73 fails (a DB error, or the process dying between the two calls), the survivor's balance has risen with no ledger entry behind it.
  - **The idempotency guard cannot repair it.** A redelivered event returns at :57 because the duplicate's balance is already 0, and the :58 check keys on the In entry that was never written. The account–ledger mismatch is permanent and silent.
  - **Not declared.** This split is not among the 18 deviations.
  - **Fix.** Enlist both new entries in one unit of work before the single commit. The ILoyaltyAccountStore or IBillableChargeRecorder "add without saving" shape fits; the alternative is one aggregate root carrying both legs. The idempotency check should key on the first-written fact.
- [MEDIUM] `src/Api/CatEcommerce.Api/Controllers/StaffPaymentsController.cs`:
  - :48 Record, :165 Approve and :200 Allocate each dispatch a command and then a query (`.ai/cqrs.md` §5 rule 1; precedent HO-FIL-001 MEDIUM).
  - Record's follow-up query builds the §12.4 response body, but Approve and Allocate re-query the payment only to learn its currency code, and fall back to a hard-coded `"BDT"`.
  - The handler result should carry the currency.
  - The command-plus-query pairs on SetCharges, the cash-up Count, loyalty Redeem/RemoveRedemption and the accounting-export download are the response shapes §12.4 specifies. They are recorded here, not raised separately.
- [MEDIUM] `src/Api/CatEcommerce.Api/Filters/BillingIdempotencyReplayFilter.cs:39` (deviation 4):
  - §12.5 says to extend `UniqueConstraintExceptionFilter` so that `ix_*_idempotency` re-dispatches the lookup. This is a second, global exception filter instead, added to MvcOptions from BillingModule.cs:57. A module now edits the host-wide MVC pipeline.
  - For the invoice, credit-note and refund cases it resolves the write-side `IBillingFinder` port directly from `RequestServices`, rather than dispatching a query.
  - It does not check that the stored record matches the replayed request. ADR-187's `IdempotencyKeyConflict` is enforced only on the sequential path.
  - Filter ordering against UniqueConstraintExceptionFilter is runtime behaviour, so I-D5 must prove it (TEST).
- [MEDIUM] `src/Infrastructure/CatEcommerce.Infrastructure/Billing/EfBankMatchRecorder.cs:21`:
  - The recorder saves the BankMatchAggregate with `db.SaveChangesAsync` outside IAggregateRootRepository. The `BankLineMatchedEvent` raised at BankMatchAggregate.cs:44 is therefore never dispatched (F31(c)), and never cleared.
  - Nothing subscribes today. A future handler would silently never fire.
  - The port is justified for the 23505 skip. It should dispatch, or document that matches raise no dispatched events.
- [MEDIUM] `src/Infrastructure/CatEcommerce.Infrastructure/Billing/CashUpRows.cs:19,61` (deviation 13):
  - `LateItemWindowDays = 60` bounds ADR-188 and §8.10's "late items … are never lost".
  - If a branch closes no cash-up for 60 days after a late item, that item never appears in any expected total.
  - It needs an Amended row and owner acceptance, or no bound.
- [MEDIUM] `src/Read/CatEcommerce.Read/QueryHandlers/Billing/ReconciliationQueryHandlers.cs:226` — `activePaymentIds` loads the PaymentId of every active bank match in the whole system, for every branch and statement, into memory on each reconciliation read. Its result is then used in a `Contains`. That set grows without limit. Use a correlated `!Any(...)` instead.
- [MEDIUM] `src/Api/CatEcommerce.Api/Controllers/StaffAccountingExportsController.cs:46` (deviation 14):
  - §12.2 specifies that `AccountingExportWriter` "streams the ZIP". It builds the whole archive in memory instead, up to `MaxExportRows` (200,000) rows across seven lists.
  - That is bounded but large, and it must be built before C26 anyway to record the SHA-256.
  - It needs an Amended row, or a temp-file stream.
- [MEDIUM] `docs/handover/HO-BIL-001.md` §1 "Amended" — none of the 18 declared deviations, nor the merge two-save split, is recorded. The row is still "(none — append rows here…)". Downstream gates need the record (accepted practice since HO-NTF-001 pass 2).
- [MEDIUM] Scope: 13 files are outside §13's literal list. All are inside the Billing area; none is §14 forbidden scope. Carried as backlog rather than an automatic fail, per the owner rule and the HO-APT-001 and HO-NTF-001 precedent. They are listed under SCOPE CHECK.
- [LOW] Several queries are capped instead of paged (UI-4):
  - `src/Read/CatEcommerce.Read/QueryHandlers/Billing/PaymentQueryHandlers.cs:176` — GetPendingApprovals is capped at MaxExportRows (200,000), not paged.
  - GetUncapturedSources (BillingChargeQueryHandlers.cs:85-104) has no cap at all, though it is bounded by the date range.
  - GetCodReconciliation (ReconciliationQueryHandlers.cs:146) is capped at 200,000.
- [LOW] `src/Api/CatEcommerce.Api/Modules/BillingModule.cs:42` — `IBankStatementCsvParser` is registered Singleton. §12.7 lists it among the Scoped registrations. It is stateless with no dependencies, so this is harmless, but it is not as specified.
- [LOW] `src/Infrastructure/CatEcommerce.Infrastructure/Fakes/Billing/InMemoryBillableChargeRecorder.cs` — the CONTRACTS.md #10 fake is no longer registered but still exists. Deleting it is outside this handover's scope, so the integrator should remove it.
- [LOW] `src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs:84` — `BillingFirstWriteTables` (billing_profile, loyalty_account) is a mapping that §12.5 does not list. It is harmless and in the right spirit, but `cash_up` is missing from it (deviation 12).
- [LOW] `src/Read/CatEcommerce.Read/QueryHandlers/Billing/AccountingExportQueryHandlers.cs:23` and BillingModule.cs:191 — a Read query's result type is an Infrastructure record (`AccountingExportContent`, and `InvoiceDocument` at InvoiceQueryHandlers.cs:208) rather than a ViewModel. This is legal, because Read references Infrastructure, but it is unusual.
- [LOW] `src/Application/CatEcommerce.Application/DomainEventHandlers/Billing/LoyaltyEventHandlers.cs:41` — the qualifying-net rule (`Lines.Where(ItemKind != Fee).Sum(NetAmount)`) is computed in the handler, although it is checkable from the InvoiceAggregate's own state. An aggregate method would keep the rule in one place; the redemption path computes the same rule.
- [LOW] Validators are grouped per area (`Billing*CommandValidators.cs`, 9 files) rather than one file per command as §13 says. The content is equivalent and each is registered as IValidator<T>.
- [LOW] Template defect (standing): `.ai/templates/*.cs` is still stale. Nothing in this diff copies from it.

SCOPE CHECK
- files in allowed scope: 249/262. Counted as in scope:
  - §13 paths, including the nine migrations with their designers and the model snapshot;
  - ADR-341 per-area translations: BillingModule.cs, CatEcommerceDbContext.Billing.cs, and the `{Actions,ActionRoles,BusinessViolationCodes,SettingDefinitions,SettingKeys}.Billing.cs` partials;
  - the C-ALL-1 file RecordClassification.Billing.cs;
  - the nine grouped validator files;
  - the cross-area lines §13 permits.
- files outside allowed scope (13). All are in-area, none is §14 forbidden, and they are carried as MEDIUM:
  - src/Api/CatEcommerce.Api/Controllers/StaffBillingControllerBase.cs
  - src/Api/CatEcommerce.Api/Controllers/StaffCashUpController.cs (§13: StaffCashUpsController)
  - src/Api/CatEcommerce.Api/Controllers/StaffLoyaltyController.cs
  - src/Api/CatEcommerce.Api/Controllers/StaffReconciliationController.cs (§13: StaffCodReconciliation + StaffBankReconciliation; StaffBillingCustomersController is also absent, its routes live elsewhere)
  - src/Api/CatEcommerce.Api/Filters/BillingIdempotencyReplayFilter.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/CodShortfall.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/IBankMatchRecorder.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/IBillingFinder.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/ILoyaltyAccountStore.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/LoyaltyCalculator.cs
  - tests/CatEcommerce.Infrastructure.Tests/Billing/AccountingExportWriterTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Billing/BankStatementCsvParserTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Persistence/BillingMigrationSqlTests.cs
- §14 is clean:
  - no `web/**`, `.claude/**`, `docs/SRS.md`, `docs/USER_STORIES.md` or `docs/handover/**`;
  - no `.ai/**` other than decisions/adr.md;
  - no `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`, and no new package;
  - no other area's aggregate, no gateway shape, no background worker and no second DbContext.

NOTES FOR LATER GATES
- **TEST — numbering (deviation 3).**
  - I-C3 runs in one process, so the in-process gate serialises most of the race. Correctness across processes rests on `xmin` and the unique number backstops.
  - Consider a two-host variant: two WebApplicationFactory instances on the same per-branch database.
  - Assert no gap and no duplicate.
- **TEST — replay filter (deviation 4).** I-D5 must prove the order in which BillingIdempotencyReplayFilter and UniqueConstraintExceptionFilter run: a parallel double-submit returns 200 with the same id, never a 500.
- **TEST — I-X1.** BillingLoggedApiFactory's provider says it captures "every log line of any level", but the host's logging filter still applies its minimum level. I-X1's scope is Information and above, so this is sufficient, but the comment overstates it.
- **TEST — merge (HIGH 2).** After the fix, I-J4 should assert account balance = Σ ledger entries for both customers.
