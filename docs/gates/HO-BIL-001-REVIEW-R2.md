GATE: ARCHITECTURE + SECURITY + CODE_REVIEW (combined follow-up, round 2)
HANDOVER: HO-BIL-001
COMMITS: 8fd5988d 7b044018 8fb004d8 a4ae560e (`git diff 0508deba..HEAD -- . ':!docs/gates'`, branch ho/BIL-001)
DATE: 2026-10-07
STATUS: PASS

Method: read the diff and the surrounding code; `dotnet build` (0 warnings, 0 errors); Domain (940), Application (236) and Infrastructure (147) unit tests all pass. Integration tests and any database were NOT touched, so EF translation of the new projections and the loyalty join is verified by reading only (see TEST note). The card guard was probed with a scratch console (~45 inputs plus 20,000 random numbers); the scratch folder is deleted.

ROUND-1 BLOCKING FINDINGS - CLOSURE
1. [SEC HIGH] card guard on every free-text money field: CLOSED.
   - Now guarded through BillingFreeText.RefuseCardNumber: payment Reject / ReleaseEarmark / Reverse reasons (RequireReason), earmark Label, credit-note Reason, cash-up Explanation, bank-match UnmatchReason, charge Void reason, manual-charge Description, corrected-draft line Description, invoice Notes. Already guarded before: payment ExternalReference and Notes, refund Reference and Reason, COD Reference.
   - I grepped every string property on the Billing aggregates and commands. The remaining unguarded free-text fields are listed in the backlog (MEDIUM 2) and were not in the round-1 list.
   - The refused value is never echoed: the exception carries the constant BillingMessages.ReferenceLooksLikeCardNumber and the property name only.
2. [SEC MEDIUM, included] guard hardening: CLOSED for the cases named in round 1. Verified by probe: double space, spaced dashes, tab, NBSP, `.`, `/`, `_`, Bengali digits, full-width digits, a glued front digit, a 15-digit Amex (4-6-5 grouping), a card inside a longer run, and two cards in one string. All are refused and masked. Mask-before-truncate and the BankTransactionId mask are in place (BankStatementAggregate.cs:73-74). Index mapping is sound: Normalise is strictly one UTF-16 char to one char, so the length never changes; verified with an emoji (surrogate pair) before a card, a full-width letter before full-width digits, and a supplementary digit. Residual bypasses go to the backlog (MEDIUM 1).
3. [ARCH HIGH] read side projects: CLOSED.
   - No Billing query handler loads an aggregate or uses ToDictionaryAsync. I scanned all 89 statements containing AsNoTracking() in QueryHandlers/Billing: none ends in ToListAsync, FirstOrDefaultAsync or SingleOrDefaultAsync without a Select or SelectMany. Dictionaries are built in memory after a projecting ToListAsync.
   - Branch, owner and status filters are carried over. Examples: invoice detail by Id and BranchId; payment detail by Id and BranchId; the customer visibility filters (VisibleToCustomer, CustomerSet) are passed unchanged into HeaderAsync; the pending-approval query keeps Take before Select.
   - Ordering is kept and made stable: entries by OccurredOn then Id, invoice and credit-note lines by SortOrder, ageing by OldestDueOn then CustomerId, invoice payments gain a ThenBy on Id.
   - No N+1: each handler runs a fixed number of queries, with Contains lookups over id lists.
   - PaymentHeader.Unallocated equals the aggregate's formula. The round-1 `activePaymentIds` MEDIUM is also fixed (correlated Any).
   - U_X6 detects the exact regression it was written for (`AsNoTracking().FirstOrDefaultAsync(`, `.ToDictionaryAsync(`). Its limits are in LOW 1.
4. [ARCH HIGH] loyalty merge transfer: CLOSED.
   - ILoyaltyAccountStore.Stage adds the In entry to the shared DbContext without saving. CommitNewAsync then saves the Out entry. Both accounts are already tracked, so one SaveChanges commits both accounts and both entries. There is no state where a balance moved without its ledger.
   - Idempotency is keyed on MergeTransferOut by SourceCustomerId, and Out and In are written in the same save, so an existing Out implies an existing In. The unique index (kind, source_customer_id) with idempotent_source rejects a racing duplicate delivery and rolls both back.
   - The In entry raises no domain events, so nothing is lost by not going through a repository save. A unit test was added (balances equal the ledger; a redelivery does nothing).
5. [SEC MEDIUM, included] accrual reversal keyed per credit note: CLOSED.
   - The reversal entry now has invoice_id NULL and credit_note_id set. ix_loyalty_entry_kind_invoice is filtered `invoice_id IS NOT NULL`, so it no longer collides. ix_loyalty_entry_kind_credit_note (kind, credit_note_id, idempotent) still stops a double clawback for the same credit note, and the handler pre-checks the same key.
   - No read depends on invoice_id of a reversal. GetAccruedNotReversed already joins through the credit note. The loyalty statement now derives the invoice from the credit note (invoiceKey). There is no loyalty export, report or DB check on that column.
   - Rows written before the fix keep their invoice_id and remain valid. No migration is needed because the indexes already existed.

REGRESSION / SCOPE CHECK
- No forbidden shape appears (no IDispatcher, aggregate rehydration in Read, service wrapper or float money). Stage is a method on an existing Domain port, and the fake was updated. All changed files are Billing files or Billing tests.
- 8fd5988d (BillingApiFactory refuses to start without CATECOMMERCE_TEST_DB, or when it names catecommerce_dev) is test infrastructure only and correct.

FINDINGS (none blocking; backlog list for docs/backlog/BIL.md)
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Helpers/CardNumberGuard.cs (Normalise) - separators still not treated as separators. Probed and NOT detected or masked: zero-width space U+200B and soft hyphen U+00AD between groups; Unicode hyphens U+2010, U+2011, U+2013 (copy-paste from PDFs or Word); `,` `:` `|`; parentheses around the first group `(4111) 1111 1111 1111`; supplementary-plane digits such as U+1D7D2 (GetDecimalDigitValue(char) cannot see surrogate pairs). Fix: map Unicode categories Pd, Cf and Zs, and `,` `:` `|` and brackets, to a space; decode digits with char.ConvertToUtf32 while keeping the output length equal. Add one theory row per character. Not HIGH: round 1 asked for the common separators, and these need deliberate evasion or unusual paste sources.
- [MEDIUM] Remaining free-text fields without the guard: ChargeAggregate.ReportingTag (<=50 chars, from the request), BillingProfile InvoiceFooter (printed on every invoice; do not guard bank account numbers), BankStatement BankAccountLabel and SourceFileName. None was named in round 1, but each is stored in a Forbidden-to-delete table or printed. Guard or mask them.
- [MEDIUM] BankStatementAggregate.cs:74 - masking BankTransactionId replaces an ordinary numeric bank transaction id of 13-20 digits that happens to pass Luhn with `••••1234`. Measured: 3,819 of 20,000 random 16-digit ids (19%, double the plain 10% because of the front-digit trim) are altered. The id is not used as a dedupe key today, so nothing breaks functionally, but the original id is lost for reconciliation. Consider keeping a hash, or masking only when the surrounding text also looks like a card.
- [LOW] False positives on ordinary text: because `.`, `/` and `_` now join digit groups, `07/10/2026 12345678` (16 digits) is masked, and a free-text reason of that shape is refused with the card-number message (10-19% of runs of 13-19 digits). Tests cover only runs that are too short. Acceptable for a safety guard; make the UI message say "remove long numbers".
- [LOW] A card number split across two fields (reference plus notes) cannot be detected by a per-field guard. Known limit; no fix proposed.
- [LOW] tests/CatEcommerce.Domain.Tests/Common/BillingConventionTests.cs U_X6 - the regex matches only `AsNoTracking().First/SingleOrDefaultAsync(` and `.ToDictionaryAsync(`. An aggregate loaded with `.AsNoTracking().Where(..).FirstOrDefaultAsync()` or `.ToListAsync()` and no Select, or with Include, is not caught. My statement scan found none today. Extend the test to flag any statement with AsNoTracking and a terminal operator but no Select/SelectMany.
- [LOW] ILoyaltyAccountStore.Stage is an unsaved-add method on a Domain port. It is documented and is the smallest way to get one save; future callers must always follow it with a save.
- [LOW] InvoiceAggregate.cs:119-121 - a stray double blank line.
- [TEST note] Runtime-only items for the TEST gate (not run here): EF translation of InvoiceHeader projecting the owned types Seller, BillTo and LoyaltyRedemption; the loyalty statement `let invoiceKey` join on a nullable key; the two-credit-note integration case (BillingLoyaltyEndpointTests); the merge integration case; BillingCardNumberEndpointTests.

SCOPE CHECK
- files in allowed scope: 33/33 (all Billing files, or Billing tests and test fakes)
- files outside allowed scope: none
