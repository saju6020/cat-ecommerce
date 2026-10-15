namespace CatEcommerce.Read.ViewModels;

// HO-IMP-001 §12.4 -- transfers, adjustments and counts (US-IMP-13, US-IMP-14).

public sealed record StockTransferSummaryViewModel(
    Guid Id, string Number, Guid FromLocationId, string? FromLocationCode, Guid ToLocationId, string? ToLocationCode, DateTimeOffset TransferredOn,
    Guid TransferredBy, int LineCount, string? Reason);

public sealed record StockTransferLineViewModel(
    Guid LineId, Guid SkuId, string SkuCode, string DisplayName, Guid BatchId, string LotNumber, string Quantity, string StockUnit);

public sealed record StockTransferDetailViewModel(
    Guid Id, string Number, Guid BranchId, Guid FromLocationId, string? FromLocationCode, Guid ToLocationId, string? ToLocationCode, string? Reason,
    DateTimeOffset TransferredOn, Guid TransferredBy, IReadOnlyList<StockTransferLineViewModel> Lines);

public sealed record StockAdjustmentSummaryViewModel(
    Guid Id, string Number, Guid LocationId, string? LocationCode, string Reason, string Status, MoneyViewModel ValueBase, Guid RequestedBy,
    DateTimeOffset RequestedOn, int LineCount);

public sealed record StockAdjustmentLineViewModel(
    Guid LineId, Guid SkuId, string SkuCode, string DisplayName, Guid BatchId, string LotNumber, string QuantityDelta, string StockUnit, MoneyViewModel UnitCostBase);

public sealed record StockAdjustmentDetailViewModel(
    Guid Id, string Number, Guid BranchId, Guid LocationId, string? LocationCode, string Reason, string Note, string Status, MoneyViewModel ValueBase, Guid RequestedBy,
    DateTimeOffset RequestedOn, Guid? DecidedBy, DateTimeOffset? DecidedOn, string? DecisionNote, IReadOnlyList<StockAdjustmentLineViewModel> Lines,
    IReadOnlyList<string> AllowedActions);

public sealed record StockCountSummaryViewModel(
    Guid Id, string Number, Guid LocationId, string? LocationCode, string Scope, string Status, int LineCount, MoneyViewModel? VarianceValueBase,
    DateTimeOffset OpenedOn, Guid OpenedBy);

/// <summary>One counted line. <c>BookQuantity</c> and <c>Variance</c> are NULL while the count is <c>Open</c>: counting is BLIND (OQ-13) -- a counter
/// never sees what the book says, only what they counted.</summary>
public sealed record StockCountLineViewModel(
    Guid SkuId, string SkuCode, string DisplayName, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, string StockUnit, string CountedQuantity,
    string? BookQuantity, string? Variance);

public sealed record StockCountDetailViewModel(
    Guid Id, string Number, Guid BranchId, Guid LocationId, string? LocationCode, string Scope, IReadOnlyList<Guid> SkuIds, string Status, DateTimeOffset OpenedOn,
    Guid OpenedBy, DateTimeOffset? SubmittedOn, MoneyViewModel? VarianceValueBase, bool IsBlind, IReadOnlyList<StockCountLineViewModel> Lines,
    IReadOnlyList<string> AllowedActions, int Version);

public sealed record StockCountVarianceLineViewModel(
    Guid SkuId, string SkuCode, string DisplayName, Guid BatchId, string LotNumber, string StockUnit, string BookQuantity, string CountedQuantity, string Variance,
    MoneyViewModel Value);

/// <summary>US-IMP-14 AC1 -- the variance report, produced after submission and before any ledger entry exists.</summary>
public sealed record StockCountVarianceViewModel(
    Guid CountId, string Number, string Status, MoneyViewModel TotalVarianceValueBase, int VarianceLineCount, bool WillNeedApproval,
    IReadOnlyList<StockCountVarianceLineViewModel> Lines);
