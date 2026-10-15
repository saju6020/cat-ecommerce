namespace CatEcommerce.Read.ViewModels;

// HO-IMP-001 §8.13 / §12.4 -- alerts, reorder and the reports (US-IMP-10, US-IMP-12).

/// <summary>What the worker's expiry pass needs to raise: one row per (batch, branch) whose tightest crossed threshold has not been raised yet, or that has expired
/// with stock and has no <c>Expired</c> alert. <c>DedupKey</c> is the key the alert is raised under.</summary>
public sealed record ExpiryAlertCandidate(Guid BranchId, Guid BatchId, Guid SkuId, int? ThresholdDays, bool IsExpired, string DedupKey);

/// <summary>A (SKU, location) pair below its effective reorder point with no live <c>LowStock</c> alert.</summary>
public sealed record LowStockCandidate(Guid BranchId, Guid SkuId, Guid LocationId, decimal Usable, decimal ReorderPoint);

public sealed record InventoryAlertViewModel(
    Guid Id, string Kind, string Status, Guid? SkuId, string? SkuCode, string? DisplayName, Guid? BatchId, string? LotNumber, Guid? LocationId, string? LocationCode,
    int? ThresholdDays, string? QuantityAtRaise, DateTimeOffset RaisedOn, Guid? AcknowledgedBy, DateTimeOffset? AcknowledgedOn, DateTimeOffset? ResolvedOn,
    string? ResolutionNote);

public sealed record OpenAlertSummaryViewModel(int Total, IReadOnlyDictionary<string, int> ByKind);

/// <summary>The navigation badge: how many alerts need a person, by kind. <c>Open</c> and <c>Acknowledged</c> alerts are both live; only <c>Open</c> ones are counted.</summary>
public sealed record StockAlertSummaryViewModel(OpenAlertSummaryViewModel Open);

public sealed record LocationQuantityViewModel(Guid LocationId, string? LocationCode, string OnHand);

/// <summary><c>DaysRemaining</c> is negative once the batch has expired; <c>ValueAtRisk</c> is on hand times the unit cost of <c>v_batch_unit_cost</c>.</summary>
public sealed record NearExpiryRowViewModel(
    Guid SkuId, string SkuCode, string DisplayName, Guid BatchId, string LotNumber, DateOnly ExpiresOn, int DaysRemaining, bool IsExpired, string Status, string Quantity,
    IReadOnlyList<LocationQuantityViewModel> Locations, MoneyViewModel UnitCostBase, string CostBasis, MoneyViewModel ValueAtRisk);

public sealed record NearExpiryReportViewModel(
    DateOnly AsOf, int WithinDays, bool IncludeExpired, MoneyViewModel TotalValueAtRisk, IReadOnlyList<NearExpiryRowViewModel> Rows);

public sealed record StockOnHandRowViewModel(
    Guid SkuId, string SkuCode, string DisplayName, string StockClass, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, string BatchStatus, Guid LocationId,
    string? LocationCode, string OnHand, string Reserved, MoneyViewModel UnitCostBase, string CostBasis, MoneyViewModel Value);

public sealed record StockOnHandReportViewModel(DateOnly AsOf, MoneyViewModel TotalValue, IReadOnlyList<StockOnHandRowViewModel> Rows);

public sealed record ReorderPolicyViewModel(
    Guid SkuId, string SkuCode, string DisplayName, Guid LocationId, string? LocationCode, string ReorderPoint, bool IsDefaulted, string? ReorderQuantity,
    Guid? PreferredSupplierId, string? PreferredSupplierName);

public sealed record ReorderSuggestionViewModel(
    Guid SkuId, string SkuCode, string DisplayName, string StockUnit, Guid LocationId, string? LocationCode, string Usable, string OnOrder, string ReorderPoint,
    bool IsDefaultedPoint, string AverageDailyConsumption, int LeadTimeDays, int CoverDays, string SuggestedQuantity, Guid? PreferredSupplierId, string? PreferredSupplierName);

public sealed record StockReconciliationRowViewModel(
    Guid BatchId, Guid LocationId, string OnHand, string ExpectedOnHand, string Reserved, string ExpectedReserved, Guid SkuId, Guid BranchId);
