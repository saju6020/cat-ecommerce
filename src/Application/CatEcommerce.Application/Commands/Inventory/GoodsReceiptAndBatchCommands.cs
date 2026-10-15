using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Inventory;

// HO-IMP-001 §11.1 C8-C9.

/// <summary>One receipt line. <see cref="BatchId"/> is minted in the controller and used only when something is accepted.</summary>
public sealed record GoodsReceiptLineInput(
    Guid LineId, Guid SkuId, Guid? PurchaseOrderLineId, string LotNumber, DateOnly? ManufacturedOn, DateOnly? ExpiresOn,
    decimal QuantityAccepted, decimal QuantityRejected, string? RejectionReason, string? RejectionNote, Guid LocationId,
    bool HoldForInspection, decimal? UnitCostAmount, string? UnitCostCurrency, decimal? ExchangeRateToBase, Guid BatchId);

/// <summary>C8 -- <c>Stock.Receive</c>. Creates the receipt, one batch per accepted line and their ledger entries in ONE commit (ADR-105).</summary>
public sealed class PostGoodsReceiptCommand : Command
{
    public required Guid GoodsReceiptId { get; init; }
    public required Guid BranchId { get; init; }
    public Guid? SupplierId { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid? ConsignmentId { get; init; }
    public string? DeliveryReference { get; init; }
    public required IReadOnlyList<GoodsReceiptLineInput> Lines { get; init; }
}

/// <summary>C9 -- <c>Batches.ManageStatus</c>.</summary>
public sealed class QuarantineBatchCommand : Command
{
    public required Guid BatchId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

public sealed class ReleaseBatchCommand : Command
{
    public required Guid BatchId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class WriteOffBatchCommand : Command
{
    public required Guid BatchId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}
