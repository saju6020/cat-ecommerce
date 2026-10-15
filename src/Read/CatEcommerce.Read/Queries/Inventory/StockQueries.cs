using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

/// <summary>HO-IMP-001 §12.4 -- goods receipts of the active branch (<c>Stock.View</c>), paged.</summary>
public sealed class GetGoodsReceiptsQuery : Query<PagedResult<GoodsReceiptSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? SupplierId { get; init; }
    public Guid? PurchaseOrderId { get; init; }
}

/// <summary><c>IncludeCosts</c> is decided by the controller from the caller's roles (<c>InventoryReports.View</c>) -- a cost never reaches a role
/// that may not see one.</summary>
public sealed class GetGoodsReceiptQuery : Query<GoodsReceiptDetailViewModel?>
{
    public required Guid GoodsReceiptId { get; init; }
    public required Guid BranchId { get; init; }
    public bool IncludeCosts { get; init; }
}

public sealed class GetStockSummaryQuery : Query<PagedResult<StockSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Search { get; init; }
    public Guid? LocationId { get; init; }
    public string? Class { get; init; }
    public bool BelowReorderPoint { get; init; }
}

public sealed class GetSkuStockMatrixQuery : Query<SkuStockMatrixViewModel?>
{
    public required Guid BranchId { get; init; }
    public required Guid SkuId { get; init; }
}

public sealed class GetBatchesQuery : Query<PagedResult<BatchSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Search { get; init; }
    public Guid? SkuId { get; init; }
    public string? Status { get; init; }
    public int? ExpiringWithinDays { get; init; }
    public Guid? ConsignmentId { get; init; }
}

public sealed class GetBatchQuery : Query<BatchDetailViewModel?>
{
    public required Guid BatchId { get; init; }
    public required Guid BranchId { get; init; }
    public bool IncludeCosts { get; init; }
}

/// <summary>US-IMP-15 AC1 -- the ledger search. <c>From</c> and <c>To</c> are REQUIRED (the controller refuses a missing or over-long range:
/// <c>InventoryRules.LedgerQueryMaxRangeDays</c>) and are inclusive UTC calendar days.</summary>
public sealed class GetLedgerQuery : Query<PagedResult<LedgerEntryViewModel>>
{
    public required Guid BranchId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public Guid? SkuId { get; init; }
    public Guid? BatchId { get; init; }
    public Guid? LocationId { get; init; }
    public string? Kind { get; init; }
    public string? ReferenceKind { get; init; }
    public Guid? ReferenceId { get; init; }
}
