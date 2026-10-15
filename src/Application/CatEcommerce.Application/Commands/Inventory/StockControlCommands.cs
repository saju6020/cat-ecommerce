using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Inventory;

// HO-IMP-001 §11.1 C10-C12.

public sealed record StockTransferLineInput(Guid LineId, Guid SkuId, Guid BatchId, decimal Quantity);

/// <summary>C10 -- <c>Stock.Transfer</c>. Two enlisted entries per line, one commit.</summary>
public sealed class PostStockTransferCommand : Command
{
    public required Guid TransferId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid FromLocationId { get; init; }
    public required Guid ToLocationId { get; init; }
    public string? Reason { get; init; }
    public required IReadOnlyList<StockTransferLineInput> Lines { get; init; }
}

public sealed record AdjustmentLineInput(Guid LineId, Guid SkuId, Guid BatchId, decimal QuantityDelta);

/// <summary>C11 -- <c>Stock.Adjust</c>. <see cref="Reason"/> and <see cref="Note"/> are both mandatory (ADR-107); the aggregate refuses
/// a missing one, so the validator only checks shape.</summary>
public sealed class RequestStockAdjustmentCommand : Command
{
    public required Guid AdjustmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid LocationId { get; init; }
    public string? Reason { get; init; }
    public string? Note { get; init; }
    public required IReadOnlyList<AdjustmentLineInput> Lines { get; init; }
}

/// <summary>C11 -- <c>Stock.ApproveAdjustment</c>.</summary>
public sealed class ApproveStockAdjustmentCommand : Command
{
    public required Guid AdjustmentId { get; init; }
    public required Guid BranchId { get; init; }
    public string? Note { get; init; }
}

public sealed class RejectStockAdjustmentCommand : Command
{
    public required Guid AdjustmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Note { get; init; }
}

/// <summary>Requester only.</summary>
public sealed class WithdrawStockAdjustmentCommand : Command
{
    public required Guid AdjustmentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed record CountLineInput(Guid SkuId, Guid BatchId, decimal CountedQuantity);

/// <summary>C12 -- <c>Stock.Count</c>.</summary>
public sealed class OpenStockCountCommand : Command
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid LocationId { get; init; }
    public required string Scope { get; init; }
    public IReadOnlyList<Guid> SkuIds { get; init; } = [];
}

public sealed class RecordCountLinesCommand : Command
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<CountLineInput> Lines { get; init; }
}

public sealed class SubmitStockCountCommand : Command
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class CommitStockCountCommand : Command
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary><c>Stock.ApproveAdjustment</c>.</summary>
public sealed class ApproveStockCountCommand : Command
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class RejectStockCountCommand : Command
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class CancelStockCountCommand : Command
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
}
