namespace CatEcommerce.Read.ViewModels;

// HO-IMP-001 §12.4 -- import consignments (US-IMP-03). Documents are METADATA plus a storage key; no file bytes are ever returned (§4.3).

public sealed record ConsignmentSummaryViewModel(
    Guid Id, string Reference, string Status, string OriginCountryCode, string ShipmentReference, DateOnly? ExpectedArrivalOn, int PurchaseOrderCount);

public sealed record HsCodeViewModel(string Code, string? Description);

public sealed record ConsignmentPurchaseOrderViewModel(Guid Id, string Number, Guid SupplierId, string SupplierName, string Status);

public sealed record CostComponentViewModel(string Kind, MoneyViewModel Amount, string? Note);

public sealed record ExchangeRateViewModel(string CurrencyCode, string RateToBase, DateOnly RecordedOn);

public sealed record ConsignmentDocumentViewModel(
    Guid DocumentId, string Kind, string? DocumentNumber, DateOnly? IssuedOn, string FileName, string StorageKey, string ContentType, long SizeInBytes,
    Guid UploadedBy, DateTimeOffset UploadedOn, bool IsSuperseded, DateTimeOffset? SupersededOn, Guid? SupersededBy);

public sealed record ConsignmentStatusChangeViewModel(string From, string To, DateTimeOffset ChangedOn, Guid ChangedBy, DateOnly? EffectiveOn);

/// <summary>One batch's landed cost (§8.11): <c>LandedUnitCostBase</c> is a four-decimal unit cost, the rest are two-decimal money in base currency.</summary>
public sealed record LandedCostLineViewModel(
    Guid BatchId, string? SkuCode, string? LotNumber, string Quantity, MoneyViewModel PurchaseCostBase, MoneyViewModel AllocatedCostBase, MoneyViewModel LandedUnitCostBase);

/// <summary><c>IsStale</c> -- the costs changed since the allocation, or the set of batches received on the consignment differs from the allocated set.</summary>
public sealed record LandedCostViewModel(
    DateTimeOffset? AllocatedOn, bool IsStale, MoneyViewModel? TotalCostsBase, IReadOnlyList<LandedCostLineViewModel> Lines);

public sealed record ConsignmentBatchViewModel(
    Guid BatchId, Guid SkuId, string SkuCode, string DisplayName, string LotNumber, DateOnly? ExpiresOn, string ReceivedQuantity, string StockUnit,
    string Status, string GoodsReceiptNumber);

public sealed record ConsignmentDetailViewModel(
    Guid Id, string Reference, Guid BranchId, string OriginCountryCode, string ShipmentReference, string? ImportPermitNumber, string? HealthCertificateNumber,
    string? CustomsDeclarationNumber, DateOnly? ExpectedArrivalOn, string Status, string? CancellationReason, DateOnly? ClearedOn, int Version,
    IReadOnlyList<HsCodeViewModel> HsCodes, IReadOnlyList<ConsignmentPurchaseOrderViewModel> PurchaseOrders, IReadOnlyList<CostComponentViewModel> CostComponents,
    IReadOnlyList<ExchangeRateViewModel> ExchangeRates, IReadOnlyList<ConsignmentDocumentViewModel> Documents, LandedCostViewModel LandedCost,
    IReadOnlyList<ConsignmentStatusChangeViewModel> StatusHistory, IReadOnlyList<string> AllowedActions);

/// <summary>US-IMP-03 AC2 -- everything together: details, HS codes, permit/certificate/declaration numbers, the POs, ALL documents (incl.
/// superseded) and the batches received with lot and expiry.</summary>
public sealed record InspectionPackViewModel(
    Guid ConsignmentId, string Reference, string Status, string OriginCountryCode, string ShipmentReference, string? ImportPermitNumber,
    string? HealthCertificateNumber, string? CustomsDeclarationNumber, DateOnly? ExpectedArrivalOn, DateOnly? ClearedOn,
    IReadOnlyList<HsCodeViewModel> HsCodes, IReadOnlyList<ConsignmentPurchaseOrderViewModel> PurchaseOrders,
    IReadOnlyList<ConsignmentDocumentViewModel> Documents, IReadOnlyList<ConsignmentBatchViewModel> Batches);
