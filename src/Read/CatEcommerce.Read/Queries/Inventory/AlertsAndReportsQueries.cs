using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

// HO-IMP-001 §12.6 -- the worker's three queries, the alert reads, the reorder reads and the reports.

/// <summary>Worker: across every ACTIVE branch, the (batch, branch, threshold) triples and expired-with-stock batches that still need an alert.</summary>
public sealed class GetExpiryAlertCandidatesQuery : Query<IReadOnlyList<ExpiryAlertCandidate>>
{
    /// <summary>Limits the pass to one branch (tests); null means every active branch.</summary>
    public Guid? BranchId { get; init; }
}

/// <summary>Worker: pairs whose usable stock is below the effective reorder point and that have no live <c>LowStock</c> alert.</summary>
public sealed class GetLowStockCandidatesQuery : Query<IReadOnlyList<LowStockCandidate>>
{
    /// <summary>Limits the pass to one branch (tests); null means every active branch.</summary>
    public Guid? BranchId { get; init; }
}

/// <summary>Worker: live <c>LowStock</c> alerts whose pair has recovered (usable at or above the point, or the point silenced).</summary>
public sealed class GetRecoveredLowStockAlertsQuery : Query<IReadOnlyList<Guid>>
{
    /// <summary>Limits the pass to one branch (tests); null means every active branch.</summary>
    public Guid? BranchId { get; init; }
}

public sealed class GetStockAlertsQuery : Query<PagedResult<InventoryAlertViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Status { get; init; }
    public string? Kind { get; init; }
    public Guid? LocationId { get; init; }
}

public sealed class GetStockAlertSummaryQuery : Query<StockAlertSummaryViewModel>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetReorderPoliciesQuery : Query<PagedResult<ReorderPolicyViewModel>>
{
    public required Guid BranchId { get; init; }
    public Guid? LocationId { get; init; }
}

public sealed class GetReorderSuggestionsQuery : Query<IReadOnlyList<ReorderSuggestionViewModel>>
{
    public required Guid BranchId { get; init; }
    public Guid? LocationId { get; init; }
    public Guid? SupplierId { get; init; }
}

/// <summary>US-IMP-10 AC2. <c>WithinDays</c> defaults to the largest expiry threshold.</summary>
public sealed class GetNearExpiryReportQuery : Query<NearExpiryReportViewModel>
{
    public required Guid BranchId { get; init; }
    public int? WithinDays { get; init; }
    public bool IncludeExpired { get; init; }
}

public sealed class GetStockOnHandReportQuery : Query<StockOnHandReportViewModel>
{
    public required Guid BranchId { get; init; }
    public Guid? LocationId { get; init; }
    public string? Class { get; init; }
}

/// <summary>§8.13 -- every position whose on hand or reserved differs from the sum of its entries. Expected empty forever.</summary>
public sealed class GetStockReconciliationQuery : Query<IReadOnlyList<StockReconciliationRowViewModel>>
{
    public Guid? BranchId { get; init; }
}
