using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>Which unbilled, active charges of one customer at one branch a selector names (§8.13): a single source,
/// or a billing group (a boarding stay) up to a service date.</summary>
public sealed record UnbilledChargeCriteria(
    Guid CustomerId, Guid? BranchId, ChargeSourceKind? SourceKind, Guid? SourceId,
    BillingGroupKind? GroupKind, Guid? GroupId, DateOnly? ServiceDateTo);

/// <summary>A payment's free credit, as a posting needs it: the payment, what is unallocated, and the
/// instant it was accepted (oldest first, ADR-185).</summary>
public sealed record FreeCreditPayment(Guid PaymentId, decimal Unallocated, DateTimeOffset AcceptedOn);

/// <summary>
/// HO-BIL-001 §11.4 -- the cross-record lookups a Billing command handler needs and the single-aggregate
/// <c>IAggregateRootRepository</c> contract cannot express (several rows at once; "does any live invoice line
/// reference this charge"). Implemented in Infrastructure over the shared <c>DbContext</c>, always
/// <c>AsNoTracking</c> id/value lookups -- the handler then loads each aggregate it will mutate through its own
/// repository (rule 5: handlers orchestrate; existence and cross-record lookups are repository-side calls).
/// The precedent is HO-APT-001's <c>IFlaggableAppointmentFinder</c>.
/// </summary>
public interface IBillingFinder
{
    // ── Charges and invoices ────────────────────────────────────────────────────────────────────────
    /// <summary>The subset of <paramref name="chargeIds"/> a line of a <b>non-discarded</b> invoice references
    /// ("billed" is derived -- ADR-178).</summary>
    Task<IReadOnlySet<Guid>> GetBilledChargeIdsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct = default);

    /// <summary>Active, unbilled charges matching the criteria, oldest service date first (C9's selector).</summary>
    Task<IReadOnlyList<Guid>> FindUnbilledChargeIdsAsync(UnbilledChargeCriteria criteria, CancellationToken ct = default);

    /// <summary>ADR-187: the invoice this caller already issued under this key, if any.</summary>
    Task<IssuedInvoiceRef?> FindInvoiceByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default);

    /// <summary>Every charge recorded for one source (any line, any status) -- Phase J voids a reversed supply's unbilled charges with it.</summary>
    Task<IReadOnlyList<Guid>> FindChargeIdsBySourceAsync(ChargeSourceKind kind, Guid sourceId, CancellationToken ct = default);

    /// <summary>Unbilled active charges and draft invoices of a customer -- what a merge moves (ADR-192).</summary>
    Task<IReadOnlyList<Guid>> FindMovableChargeIdsAsync(Guid customerId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> FindDraftInvoiceIdsAsync(Guid customerId, CancellationToken ct = default);

    // ── Payments ────────────────────────────────────────────────────────────────────────────────────
    /// <summary>ADR-187: the payment this caller already recorded under this key, if any.</summary>
    Task<RecordedPaymentRef?> FindPaymentByIdempotencyKeyAsync(Guid recordedBy, string key, CancellationToken ct = default);

    /// <summary>The next receipt number from <c>billing.receipt_number_seq</c>. Gaps are allowed (a receipt number is not a legal
    /// document number), so a number taken for a request that then fails is simply never used. Taken <b>before</b> the save so the
    /// payment-received message can carry it.</summary>
    Task<long> NextReceiptNumberAsync(CancellationToken ct = default);

    // ── Credit notes and refunds ────────────────────────────────────────────────────────────────────
    /// <summary>What earlier credit notes already took from each line of an invoice (quantity only for quantity credits).</summary>
    Task<IReadOnlyDictionary<Guid, CreditedSoFar>> GetCreditedSoFarAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>The payments with a positive net allocation to this invoice, <b>newest allocation first</b> -- the order a credit-note
    /// excess is released in (§8.9 step 3, U-E2).</summary>
    Task<IReadOnlyList<Guid>> FindPaymentIdsAllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct = default);

    Task<IssuedCreditNoteRef?> FindCreditNoteByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default);

    Task<IssuedRefundRef?> FindRefundByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default);

    /// <summary>A live (non-discarded) corrected invoice already made from this credit note, if any.</summary>
    Task<Guid?> FindCorrectingInvoiceIdAsync(Guid creditNoteId, CancellationToken ct = default);

    // ── Cash-up ─────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The day's payments, refunds and reversals at the branch, plus the late items of earlier closed days (carried in).</summary>
    Task<IReadOnlyList<CashUpRow>> GetCashUpRowsAsync(Guid branchId, DateOnly businessDate, CancellationToken ct = default);

    /// <summary>Payments still awaiting approval that were received, or recorded in branch time, on the date.</summary>
    Task<int> CountPendingApprovalsAsync(Guid branchId, DateOnly businessDate, string timeZoneId, CancellationToken ct = default);

    // ── Reconciliation (COD remittances, bank statements) ───────────────────────────────────────────
    /// <summary>Σ of remitted amounts per COD payment, across every remittance (a payment never remitted is absent).</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetRemittedTotalsAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct = default);

    /// <summary>True when this exact file (by SHA-256) was already imported for the bank account.</summary>
    Task<bool> StatementExistsAsync(string bankAccountLabel, string sha256, CancellationToken ct = default);

    /// <summary>Accepted bank-transfer payments at the branch received in the range and not actively matched.</summary>
    Task<IReadOnlyList<TransferPaymentCandidate>> FindUnmatchedTransferPaymentsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> GetActivelyMatchedLineIdsAsync(Guid statementId, CancellationToken ct = default);

    Task<bool> IsPaymentActivelyMatchedAsync(Guid paymentId, CancellationToken ct = default);

    // ── Loyalty and customer credit ─────────────────────────────────────────────────────────────────
    /// <summary>True when the ledger already holds an idempotent-source entry of this kind for the invoice, credit note or merged customer.</summary>
    Task<bool> LoyaltyEntryExistsAsync(LoyaltyEntryKind kind, Guid? invoiceId, Guid? creditNoteId, Guid? sourceCustomerId, CancellationToken ct = default);

    /// <summary>Points accrued on the invoice minus points already taken back by its credit notes (never below zero).</summary>
    Task<int> GetAccruedNotReversedAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Accepted payments of the customer (and customers merged into them) with free credit left -- not earmarked, or the earmark released --
    /// oldest acceptance first, the order customer credit is drawn in (C13, U-I6).</summary>
    Task<IReadOnlyList<Guid>> FindFreeCreditPaymentIdsAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Every credit note issued against the invoice.</summary>
    Task<IReadOnlyList<Guid>> FindCreditNoteIdsAsync(Guid invoiceId, CancellationToken ct = default);
}

public sealed record IssuedInvoiceRef(Guid InvoiceId, Guid CustomerId, string Number);

public sealed record RecordedPaymentRef(Guid PaymentId, Guid CustomerId);

public sealed record CreditedSoFar(Guid InvoiceLineId, decimal Quantity, decimal Net, decimal Tax, decimal Gross);

public sealed record IssuedCreditNoteRef(Guid CreditNoteId, Guid InvoiceId, Guid CustomerId, string Number, decimal ReleasedToCustomerCredit);

public sealed record IssuedRefundRef(Guid RefundId, Guid PaymentId, Guid CustomerId);
