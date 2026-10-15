using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Order;

/// <summary>HO-ORD-001 §11.1 C10 (FR-ORD-22, US-ORD-19) -- the customer says how they paid: the reference their bank gave the transfer. Own order only.</summary>
public sealed class SubmitTransferReferenceCommand : Command
{
    public required Guid OrderId { get; init; }
    public required string Reference { get; init; }
}

/// <summary>
/// C11 (US-ORD-19 AC2, US-ORD-08, FR-BIL-12) -- staff verified the money in the bank record and enter the <b>exact</b> amount, the bank reference and when
/// it arrived. <see cref="BranchId"/> is the staff member's active branch (an order of another branch is not found). Above Billing's approval
/// threshold the confirmation is <b>held</b> for a second person (ADR-204).
/// </summary>
public sealed class ConfirmBankTransferCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required decimal Amount { get; init; }
    public required string BankReference { get; init; }
    public required DateTimeOffset ReceivedOn { get; init; }
}

public sealed record ConfirmBankTransferResult(string Status, bool AwaitingApproval);

/// <summary>C12 -- a <b>different</b> person approves the held confirmation; only then are the invoice, the payment and the confirmation committed.</summary>
public sealed class ApproveHeldPaymentCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
}

public sealed record ApproveHeldPaymentResult(string Status);

public sealed class RejectHeldPaymentCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Note { get; init; }
}

/// <summary>C13 (BR-16, FR-ORD-22) -- sent by the <c>OrderWorker</c> only: expire one unpaid bank-transfer order past its payment window.</summary>
public sealed class ExpireUnpaidOrderCommand : Command
{
    public required Guid OrderId { get; init; }
}

/// <summary>
/// C14 (US-ORD-12) -- cancel an order. A customer cancels their own order before packing starts; staff (<see cref="BranchId"/> = their active branch) also
/// while packing, with a <see cref="Note"/> saying why. Stock is released, the discount use given back, the invoice (if any) credited in full.
/// </summary>
public sealed class CancelOrderCommand : Command
{
    public required Guid OrderId { get; init; }
    public Guid? BranchId { get; init; }
    public string? Note { get; init; }
}

/// <summary>C15 (US-ORD-19 AC3, US-POR-02) -- put the available lines of an earlier order back in the customer's cart at today's prices. Creates no order.</summary>
public sealed class ReorderCommand : Command
{
    public required Guid SourceOrderId { get; init; }
}

public sealed record UnavailableReorderLine(string ProductName, string Reason);

public sealed record ReorderResult(int AddedCount, IReadOnlyList<UnavailableReorderLine> Unavailable);

/// <summary>C27 (US-ORD-08 AC2) -- an accountant closes a reconciliation issue, saying what was done.</summary>
public sealed class ResolveReconciliationIssueCommand : Command
{
    public required Guid IssueId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Note { get; init; }
}
