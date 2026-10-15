namespace CatEcommerce.Read.ViewModels;

// HO-IMP-001 §12.4 -- goods receipts, stock, batches and the ledger. Quantities are strings with the stock unit beside them; every batch view
// carries isExpired, daysToExpiry and branchTimeZoneId (expiry is DERIVED in the branch's local date, never stored -- ADR-101); every SKU,
// receipt, batch and stock read carries `storage` (US-IMP-16 AC1). The ledger view has NO customer or cat field -- those are in the recall
// trace only (ADR-104).

public sealed record GoodsReceiptSummaryViewModel(
    Guid Id, string Number, DateTimeOffset ReceivedOn, Guid? SupplierId, string? SupplierName, Guid? PurchaseOrderId, string? PurchaseOrderNumber,
    Guid? ConsignmentId, string? DeliveryReference, int LineCount, int BatchCount, Guid ReceivedBy);

public sealed record GoodsReceiptLineViewModel(
    Guid LineId, Guid SkuId, string SkuCode, string DisplayName, string StockUnit, string LotNumber, DateOnly? ManufacturedOn, DateOnly? ExpiresOn,
    string QuantityAccepted, string QuantityRejected, string? RejectionReason, string? RejectionNote, Guid LocationId, string? LocationCode, bool HoldForInspection,
    MoneyViewModel? UnitCost, Guid? BatchId, string? BatchStatus, StorageViewModel? Storage);

public sealed record GoodsReceiptDetailViewModel(
    Guid Id, string Number, Guid BranchId, DateTimeOffset ReceivedOn, Guid ReceivedBy, Guid? SupplierId, string? SupplierName, Guid? PurchaseOrderId,
    string? PurchaseOrderNumber, Guid? ConsignmentId, string? ConsignmentReference, string? DeliveryReference, IReadOnlyList<GoodsReceiptLineViewModel> Lines);

/// <summary>One SKU's stock across the branch, partitioned so the buckets never overlap: <c>HeldQuantity</c> (a quarantined or recalled batch, or
/// anything at a quarantine location), <c>ExpiredQuantity</c> (derived), and the rest, of which <c>Usable</c> is on-hand minus reserved and
/// <c>Sellable</c> is the usable quantity at retail-selling locations (what the storefront may offer).</summary>
public sealed record StockSummaryViewModel(
    Guid SkuId, string SkuCode, string DisplayName, string StockUnit, string StockClass, string OnHand, string Reserved, string Usable, string Sellable,
    string ExpiredQuantity, string HeldQuantity, DateOnly? NextExpiresOn, StorageViewModel? Storage);

public sealed record StockLocationQuantityViewModel(Guid LocationId, string LocationCode, string LocationKind, string OnHand, string Reserved);

public sealed record StockMatrixBatchViewModel(
    Guid BatchId, string LotNumber, DateOnly? ExpiresOn, bool IsExpired, int? DaysToExpiry, string Status, IReadOnlyList<StockLocationQuantityViewModel> Locations);

/// <summary>US-IMP-06 -- the batch x location matrix of one SKU.</summary>
public sealed record SkuStockMatrixViewModel(
    Guid SkuId, string SkuCode, string DisplayName, string StockUnit, StorageViewModel? Storage, string BranchTimeZoneId, IReadOnlyList<StockMatrixBatchViewModel> Batches);

public sealed record BatchSummaryViewModel(
    Guid Id, Guid SkuId, string SkuCode, string DisplayName, string LotNumber, DateOnly? ExpiresOn, bool IsExpired, int? DaysToExpiry, string BranchTimeZoneId,
    string Status, string ReceivedQuantity, string OnHand, string StockUnit, Guid? ConsignmentId);

public sealed record BatchPositionViewModel(Guid LocationId, string LocationCode, string LocationKind, string OnHand, string Reserved);

public sealed record BatchStatusChangeViewModel(string From, string To, string? Reason, DateTimeOffset ChangedOn, Guid ChangedBy);

public sealed record BatchRecallViewModel(
    string RecallNumber, string Reason, string CustomerAdvice, DateTimeOffset DeclaredOn, Guid DeclaredBy, DateTimeOffset? NotifiedOn, Guid? NotifiedBy,
    int NotifiedRecipientCount, int NotificationRuns, DateTimeOffset? ClosedOn, string? ClosingNote);

/// <summary><c>UnitCostBase</c>, <c>CostBasis</c> and <c>PurchaseUnitCost</c> are null for a caller without <c>InventoryReports.View</c>.</summary>
public sealed record BatchDetailViewModel(
    Guid Id, Guid SkuId, string SkuCode, string DisplayName, string StockUnit, string StockClass, Guid BranchId, string LotNumber, DateOnly? ManufacturedOn,
    DateOnly? ExpiresOn, bool IsExpired, int? DaysToExpiry, string BranchTimeZoneId, string Status, string? StatusReason, DateTimeOffset ReceivedOn,
    string ReceivedQuantity, Guid GoodsReceiptId, string? GoodsReceiptNumber, Guid? ConsignmentId, string? ConsignmentReference, Guid? SupplierId, string? OriginCountryCode,
    MoneyViewModel? PurchaseUnitCost, MoneyViewModel? UnitCostBase, string? CostBasis, IReadOnlyList<BatchPositionViewModel> Positions, BatchRecallViewModel? Recall,
    IReadOnlyList<BatchStatusChangeViewModel> StatusHistory, IReadOnlyList<string> AllowedActions, StorageViewModel? Storage, int Version);

public sealed record LedgerEntryViewModel(
    long Sequence, DateTimeOffset OccurredOn, string Kind, string QuantityDelta, string StockUnit, Guid LocationId, string? LocationCode, Guid BatchId, string? LotNumber,
    Guid SkuId, string? SkuCode, string ReferenceKind, Guid ReferenceId, string? ReferenceLabel, bool FefoOverridden, string? OverrideReason, Guid PerformedBy,
    string? PerformedByName, string? Note);
