using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Order;

// HO-ORD-001 §11.1 C22-C25 (Phase G). A customer acts on their own order; staff act on the orders of their ACTIVE branch (BranchId), so another branch's order or
// return does not exist for them (404).

public sealed record RequestedReturnLine(Guid OrderLineId, int Quantity, ReturnReason Reason, ItemCondition Condition, string? CustomerNote);

/// <summary>C22 (US-ORD-13) -- a return request. From a customer it is their own order; staff may file one on the customer's behalf (<see cref="BranchId"/> = their active
/// branch). A request with any refused line is refused whole, with a per-line reason.</summary>
public sealed class RequestReturnCommand : Command
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid OrderId { get; init; }
    public Guid? BranchId { get; init; }
    public required IReadOnlyList<RequestedReturnLine> Lines { get; init; }
}

public sealed record RequestReturnResult(Guid ReturnRequestId, string Status);

public sealed record ApprovedReturnLine(Guid OrderLineId, bool DefectConfirmed, string? DefectNote);

/// <summary>C22 -- staff approve. A line that was opened or used (BR-10) needs <c>DefectConfirmed</c> with a note.</summary>
public sealed class ApproveReturnCommand : Command
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid BranchId { get; init; }
    public IReadOnlyList<ApprovedReturnLine> Lines { get; init; } = [];
    public string? Note { get; init; }
}

public sealed class RejectReturnCommand : Command
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Note { get; init; }
}

public sealed record ReceivedReturnLine(Guid OrderLineId, int QuantityReceived);

/// <summary>C23 (US-ORD-14 AC1) -- the goods arrived: they go into the branch's quarantine location (never sellable), and the order counts them as returned.</summary>
public sealed class ReceiveReturnCommand : Command
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<ReceivedReturnLine> Lines { get; init; }
}

/// <summary>C25 (US-ORD-15) -- pay money back. The method must be allowed by how the customer paid; <c>CashInStore</c> needs the receipt number and
/// <c>BankTransfer</c> the transfer reference (never a card number). A Billing credit note is raised (or the cancellation's linked) in the same unit of work.</summary>
public sealed class IssueRefundCommand : Command
{
    public required Guid RefundId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public Guid? ReturnRequestId { get; init; }
    public required OrderRefundMethod Method { get; init; }
    public required decimal Amount { get; init; }
    public string? Reference { get; init; }
    public required string Reason { get; init; }
}

public sealed record IssueRefundResult(Guid RefundId, Guid? CreditNoteId);
