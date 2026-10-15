using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Billing;

// HO-BIL-001 §11.5: ids, instants, dates, enums, booleans and decimal amounts only -- NO strings (no
// reasons, references, names, labels). U-X1 reflects over this namespace. A reason lives on the aggregate
// and is read back by AuditBillingDecisionHandler (the HO-ADM-001 §11.6 pattern).

public sealed class BillingProfileChangedEvent : Event
{
    public required Guid BranchId { get; init; }
}

public sealed class DraftInvoiceCreatedEvent : Event
{
    public required Guid InvoiceId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class DraftInvoiceChangedEvent : Event
{
    public required Guid InvoiceId { get; init; }
}

public sealed class DraftInvoiceDiscardedEvent : Event
{
    public required Guid InvoiceId { get; init; }
}

/// <summary>Published contract (§8.13): consumers' handlers must be idempotent and must not enqueue.</summary>
public sealed class InvoiceIssuedEvent : Event
{
    public required Guid InvoiceId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required decimal Total { get; init; }
}

/// <summary>Published contract (§8.13). Raised only when the derived status actually changes.</summary>
public sealed class InvoiceStatusChangedEvent : Event
{
    public required Guid InvoiceId { get; init; }
    public required InvoiceStatus From { get; init; }
    public required InvoiceStatus To { get; init; }
}

public sealed class PaymentRecordedEvent : Event
{
    public required Guid PaymentId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required PaymentMethod Method { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>FU1 D4 (ORD-52): the second approval was waived for this payment; the reason is on the payment and in the audit row (no string on an event).</summary>
public sealed class PaymentApprovalWaivedEvent : Event
{
    public required Guid PaymentId { get; init; }
}

public sealed class PaymentApprovedEvent : Event
{
    public required Guid PaymentId { get; init; }
}

public sealed class PaymentRejectedEvent : Event
{
    public required Guid PaymentId { get; init; }
}

/// <summary>Published contract (§8.13) -- HO-ORD-001 confirms a transfer order, HO-BRD-001 a deposit.</summary>
public sealed class PaymentAcceptedEvent : Event
{
    public required Guid PaymentId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required PaymentMethod Method { get; init; }
    public required decimal Amount { get; init; }
    public required EarmarkKind? EarmarkKind { get; init; }
    public required Guid? EarmarkId { get; init; }
}

public sealed class PaymentAllocatedEvent : Event
{
    public required Guid PaymentId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class PaymentAllocationReleasedEvent : Event
{
    public required Guid PaymentId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class PaymentEarmarkReleasedEvent : Event
{
    public required Guid PaymentId { get; init; }
}

/// <summary>Published contract (§8.13).</summary>
public sealed class PaymentReversedEvent : Event
{
    public required Guid PaymentId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>Published contract (§8.13).</summary>
public sealed class CreditNoteIssuedEvent : Event
{
    public required Guid CreditNoteId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required decimal Total { get; init; }
    public required decimal ReleasedToCustomerCredit { get; init; }
}

/// <summary>Published contract (§8.13).</summary>
public sealed class RefundIssuedEvent : Event
{
    public required Guid RefundId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid? CreditNoteId { get; init; }
    public required Guid CustomerId { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class CashUpClosedEvent : Event
{
    public required Guid CashUpId { get; init; }
    public required Guid BranchId { get; init; }
    public required DateOnly BusinessDate { get; init; }
    public required decimal Variance { get; init; }
}

public sealed class CodRemittanceRecordedEvent : Event
{
    public required Guid RemittanceId { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class BankStatementImportedEvent : Event
{
    public required Guid StatementId { get; init; }
    public required int LineCount { get; init; }
}

public sealed class BankLineMatchedEvent : Event
{
    public required Guid MatchId { get; init; }
    public required Guid StatementLineId { get; init; }
    public required Guid PaymentId { get; init; }
}

public sealed class BankLineUnmatchedEvent : Event
{
    public required Guid MatchId { get; init; }
}

public sealed class LoyaltyPointsAccruedEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required int Points { get; init; }
}

public sealed class LoyaltyPointsRedeemedEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required int Points { get; init; }
}

public sealed class AccountingExportRecordedEvent : Event
{
    public required Guid RunId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
}
