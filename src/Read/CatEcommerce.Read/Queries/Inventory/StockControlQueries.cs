using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

/// <summary>HO-IMP-001 §12.4 -- transfers, adjustments and counts of the ACTIVE branch (<c>Stock.View</c>), paged.</summary>
public sealed class GetStockTransfersQuery : Query<PagedResult<StockTransferSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetStockTransferQuery : Query<StockTransferDetailViewModel?>
{
    public required Guid TransferId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetStockAdjustmentsQuery : Query<PagedResult<StockAdjustmentSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Status { get; init; }
}

public sealed class GetStockAdjustmentQuery : Query<StockAdjustmentDetailViewModel?>
{
    public required Guid AdjustmentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetStockCountsQuery : Query<PagedResult<StockCountSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Status { get; init; }
    public Guid? LocationId { get; init; }
}

/// <summary>The count sheet -- BLIND while the count is <c>Open</c> (no book quantity, no variance in any line).</summary>
public sealed class GetStockCountSheetQuery : Query<StockCountDetailViewModel?>
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>US-IMP-14 AC1 -- the frozen variance report; null until the count has been submitted.</summary>
public sealed class GetStockCountVarianceQuery : Query<StockCountVarianceViewModel?>
{
    public required Guid CountId { get; init; }
    public required Guid BranchId { get; init; }
}
