namespace CatEcommerce.Read.ViewModels;

// ── Payments (HO-BIL-001 §12.4) ─────────────────────────────────────────────────────────────────────
public sealed record PaymentSummaryViewModel(
    Guid PaymentId, string? ReceiptNumber, Guid CustomerId, string CustomerName, Guid BranchId, string Method, string Status, MoneyViewModel Amount,
    MoneyViewModel Unallocated, DateOnly ReceivedOn, DateTimeOffset RecordedOn, Guid RecordedBy, string RecordedByName, string? ExternalReference, int Version);

public sealed record PaymentCollectorViewModel(string Kind, Guid? StaffAccountId, string? StaffName, string? CourierCode, string? CourierName);

public sealed record PaymentEarmarkViewModel(
    string Kind, Guid SourceId, string Label, bool IsReleased, DateTimeOffset? ReleasedOn, string? ReleaseReason);

public sealed record PaymentEntryViewModel(
    Guid EntryId, string Kind, Guid InvoiceId, string? InvoiceNumber, MoneyViewModel Amount, DateTimeOffset OccurredOn, Guid By, string? ReleaseReason);

public sealed record PaymentRefundViewModel(Guid RefundId, string Method, MoneyViewModel Amount, DateOnly BusinessDate, Guid? CreditNoteId, string? CreditNoteNumber);

public sealed record PaymentApprovalTrailViewModel(
    Guid RecordedBy, string RecordedByName, DateTimeOffset RecordedOn, Guid? ApprovedBy, string? ApprovedByName, DateTimeOffset? ApprovedOn,
    Guid? RejectedBy, string? RejectedByName, DateTimeOffset? RejectedOn, string? RejectionReason, Guid? ReversedBy, string? ReversedByName,
    DateTimeOffset? ReversedOn, string? ReversalReason);

public sealed record RequestedAllocationViewModel(Guid InvoiceId, string? InvoiceNumber, MoneyViewModel Amount);

public sealed record PaymentDetailViewModel(
    PaymentSummaryViewModel Summary, string? Notes, DateTimeOffset? AcceptedOn, DateOnly? BusinessDate, PaymentCollectorViewModel? Collector,
    PaymentEarmarkViewModel? Earmark, IReadOnlyList<PaymentEntryViewModel> Entries, IReadOnlyList<PaymentRefundViewModel> Refunds,
    PaymentApprovalTrailViewModel Trail, MoneyViewModel Refunded, IReadOnlyList<RequestedAllocationViewModel> RequestedAllocations, IReadOnlyList<string> AllowedActions);

/// <summary>The data a receipt prints: the branch's billing profile (the seller), the receipt number, who paid, how, how much, and what it
/// went towards.</summary>
public sealed record PaymentReceiptViewModel(
    string? ReceiptNumber, SellerViewModel? Seller, string BranchName, string CustomerName, string Method, string? Reference, MoneyViewModel Amount,
    DateOnly ReceivedOn, DateTimeOffset? AcceptedOn, string Status, IReadOnlyList<string> AppliedTo, string RecordedByName, string BranchTimeZoneId);

public sealed record PendingApprovalViewModel(
    Guid PaymentId, Guid CustomerId, string CustomerName, string Method, MoneyViewModel Amount, DateOnly ReceivedOn, DateTimeOffset RecordedOn,
    Guid RecordedBy, string RecordedByName, string? ExternalReference, bool CanApprove, IReadOnlyList<RequestedAllocationViewModel> RequestedAllocations);

public sealed record PaymentInvoiceOutstandingViewModel(Guid InvoiceId, string? Number, MoneyViewModel Outstanding, string Status);

public sealed record AppliedAllocationViewModel(Guid InvoiceId, string? Number, MoneyViewModel Amount);

/// <summary>The body of <c>POST /api/staff/billing/payments</c> -- built from the stored payment, so a replay answers identically.</summary>
public sealed record PaymentRecordResultViewModel(
    Guid PaymentId, string Status, string? ReceiptNumber, IReadOnlyList<AppliedAllocationViewModel> Applied, MoneyViewModel Unallocated,
    IReadOnlyList<PaymentInvoiceOutstandingViewModel> InvoiceOutstanding);

// ── Receivables ─────────────────────────────────────────────────────────────────────────────────────
public sealed record EarmarkedCreditViewModel(Guid PaymentId, string Label, MoneyViewModel Amount);

public sealed record CustomerBillingSummaryViewModel(
    MoneyViewModel Outstanding, MoneyViewModel Overdue, MoneyViewModel FreeCredit, IReadOnlyList<EarmarkedCreditViewModel> EarmarkedCredit,
    MoneyViewModel UnbilledTotal, int? LoyaltyPoints, bool LoyaltyEnabled, DateOnly? OldestDueOn);

public sealed record AgeingTotalsViewModel(MoneyViewModel Current, MoneyViewModel Days1To30, MoneyViewModel Days31To60, MoneyViewModel Days61To90, MoneyViewModel Over90);

public sealed record AgeingRowViewModel(
    Guid CustomerId, string CustomerName, MoneyViewModel Current, MoneyViewModel Days1To30, MoneyViewModel Days31To60, MoneyViewModel Days61To90,
    MoneyViewModel Over90, MoneyViewModel Total, DateOnly OldestDueOn);

public sealed record AgeingViewModel(DateOnly AsOf, AgeingTotalsViewModel Totals, PagedResult<AgeingRowViewModel> Rows);

// ── Published read contracts (§8.13) ────────────────────────────────────────────────────────────────
public sealed record BillingGroupLineViewModel(string BusinessLine, int Charges, MoneyViewModel Billed, MoneyViewModel Unbilled);

public sealed record BillingGroupSummaryViewModel(
    IReadOnlyList<BillingGroupLineViewModel> ByBusinessLine, MoneyViewModel BilledTotal, MoneyViewModel UnbilledTotal, MoneyViewModel InvoicedOutstanding,
    IReadOnlyList<EarmarkedCreditViewModel> EarmarkedPayments, IReadOnlyList<Guid> InvoiceIds);

public sealed record SourceBillingInvoiceViewModel(Guid InvoiceId, string? Number, string Status, MoneyViewModel Outstanding);

public sealed record SourceBillingStatusViewModel(
    int Charges, int BilledCharges, int UnbilledCharges, int VoidedCharges, MoneyViewModel Total, IReadOnlyList<SourceBillingInvoiceViewModel> Invoices, bool FullyPaid);
