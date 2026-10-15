using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>Who physically took the money: a staff member, or a courier (code and name snapshot, OQ-11).</summary>
public sealed record PaymentCollectorInput(string Kind, Guid? StaffAccountId = null, string? CourierCode = null, string? CourierName = null);

/// <summary>A deposit: money set aside for one order or boarding stay (<c>Kind</c> is an <c>EarmarkKind</c> name).</summary>
public sealed record PaymentEarmarkInput(string Kind, Guid SourceId, string Label);

public sealed record PaymentAllocationInput(Guid InvoiceId, decimal Amount);

/// <summary>
/// HO-BIL-001 §11.1 C10 (US-BIL-11, 03, 04) -- record money received. <b>The published contract of §8.13</b>: HO-ORD-001 sends it
/// for cash on delivery (with a courier or staff collector), a confirmed bank transfer and POS tender; HO-BRD-001 for a deposit
/// (an <see cref="Earmark"/>). Method-dependent requirements (reference, collector) are read from the method's flags; above the
/// branch's approval threshold the payment waits for a different staff member (ADR-183).
/// </summary>
public sealed class RecordPaymentCommand : Command
{
    public required Guid PaymentId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }

    /// <summary>A <c>PaymentMethod</c> name -- <c>CashInStore</c>, <c>CardInStore</c>, <c>BankTransfer</c> or <c>CashOnDelivery</c>.</summary>
    public required string Method { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly ReceivedOn { get; init; }
    public string? ExternalReference { get; init; }
    public PaymentCollectorInput? Collector { get; init; }
    public PaymentEarmarkInput? Earmark { get; init; }
    public IReadOnlyList<PaymentAllocationInput> Allocations { get; init; } = [];
    public string? Notes { get; init; }
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Additive (HO-ORD-001-FU1 D4, owner decision 2026-10-10; <c>CONTRACTS.md</c> #29). Set only by an in-process producer that has already decided the payment needs no
    /// second approver -- Ordering's cash-on-delivery collection (<c>CodCollection</c>) and counter cash/card tenders (<c>CounterTender</c>) (HO-ORD-001 OQ-7). The payment
    /// is then accepted at once whatever its size, and the reason is stored on it and audited. Billing refuses any other method/reason pairing
    /// (<c>ApprovalWaiverNotAllowed</c>). Never bound from an HTTP request: <c>StaffPaymentsController</c> builds the command field by field (BR-17).
    /// </summary>
    public string? ApprovalWaiverReason { get; init; }
}

/// <summary>C11 -- a <b>different</b> staff member with <c>Payments.Approve</c> accepts a pending payment (ADR-183).</summary>
public sealed class ApprovePaymentCommand : Command
{
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class RejectPaymentCommand : Command
{
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C12 -- apply an accepted payment's unallocated money to invoices (US-BIL-03, 04).</summary>
public sealed class AllocatePaymentCommand : Command
{
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<PaymentAllocationInput> Allocations { get; init; }
}

/// <summary>C14 -- a deposit that will not be used for its order or stay becomes free credit.</summary>
public sealed class ReleasePaymentEarmarkCommand : Command
{
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C15 -- a mistake is reversed and re-entered, never edited (US-BIL-11 AC3).</summary>
public sealed class ReversePaymentCommand : Command
{
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}
