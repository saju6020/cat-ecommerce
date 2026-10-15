using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

// HO-IMP-001 §11.3 -- the Domain DTOs of the Inventory context. One file, like AppointmentDtos.cs. Every command
// handler maps its command to one of these; an aggregate takes nothing else.

/// <summary>Who did it and under which correlation id -- carried by every DTO so an aggregate's domain event can
/// name the actor (the audit capture reads it).</summary>
public sealed record InventoryActor(UserContext UserContext, Guid CorrelationId);

/// <summary>What a SKU looked like when it was ordered or received -- reads never join the catalogue for a name.</summary>
public sealed record SkuSnapshot(string SkuCode, string DisplayName, string StockUnit, StockClass StockClass);

/// <summary>HO-IMP-001 §8.2 -- the one shape Inventory knows of a catalogue SKU (ADR-111).</summary>
public sealed record SkuStockProfile(
    Guid SkuId, Guid ProductId, string SkuCode, string DisplayName, string? Barcode,
    StockClass StockClass,
    bool ExpiryRequired,
    string StockUnit,
    bool AllowsFractionalQuantity,
    string? CountryOfOriginCode,
    bool IsActive)
{
    public SkuSnapshot Snapshot => new(SkuCode, DisplayName, StockUnit, StockClass);
}

public sealed record StockLocationCreateDto(Guid BranchId, string Code, string Name, StockLocationKind Kind, InventoryActor Actor);

public sealed record StorageProfileDto(
    StorageCondition Condition, decimal? MinTemperatureC, decimal? MaxTemperatureC, string? HandlingNotes, InventoryActor Actor);

public sealed record SupplierDto(
    string Code, string Name, string CountryCode, string? ContactName, string? Phone, string? Email, string? Address,
    string CurrencyCode, int LeadTimeDays, string PaymentTerms, int? PaymentTermDays, string? Notes, InventoryActor Actor);

public sealed record PurchaseOrderLineDto(
    Guid LineId, Guid SkuId, SkuSnapshot Sku, bool AllowsFractionalQuantity, decimal OrderedQuantity, decimal UnitCostAmount);

public sealed record PurchaseOrderDraftDto(
    string Number, Guid SupplierId, Guid BranchId, Guid DeliverToLocationId, string CurrencyCode, DateOnly ExpectedDeliveryOn,
    string? Notes, IReadOnlyList<PurchaseOrderLineDto> Lines, InventoryActor Actor);

public sealed record HsCodeDto(string Code, string? Description);

public sealed record ConsignmentDetailsDto(
    string OriginCountryCode, string ShipmentReference, IReadOnlyList<Guid> PurchaseOrderIds, IReadOnlyList<HsCodeDto> HsCodes,
    string? ImportPermitNumber, string? HealthCertificateNumber, string? CustomsDeclarationNumber, DateOnly? ExpectedArrivalOn,
    InventoryActor Actor);

public sealed record ConsignmentPlanDto(string Reference, Guid BranchId, DateTimeOffset PlannedOn, ConsignmentDetailsDto Details);

public sealed record ConsignmentDocumentDto(
    Guid DocumentId, ImportDocumentKind Kind, string? DocumentNumber, DateOnly? IssuedOn, string FileName, string StorageKey,
    string ContentType, long SizeInBytes, DateTimeOffset UploadedOn, InventoryActor Actor);

public sealed record CostComponentDto(CostComponentKind Kind, decimal Amount, string CurrencyCode, string? Note);

public sealed record ExchangeRateDto(string CurrencyCode, decimal RateToBase, DateOnly RecordedOn);

public sealed record ConsignmentCostsDto(
    IReadOnlyList<CostComponentDto> CostComponents, IReadOnlyList<ExchangeRateDto> ExchangeRates, InventoryActor Actor);

public sealed record LandedCostLineDto(Guid BatchId, decimal Quantity, decimal PurchaseCostBase, decimal AllocatedCostBase, decimal LandedUnitCostBase);

public sealed record LandedCostAllocationDto(
    IReadOnlyList<LandedCostLineDto> Lines, DateTimeOffset AllocatedOn, InventoryActor Actor);

/// <summary>The input the command handler hands the aggregate to mint a batch (§8.6, ADR-105).</summary>
public sealed record NewBatchDto(
    Guid BatchId, Guid BranchId, Guid GoodsReceiptId, Guid GoodsReceiptLineId, Guid? ConsignmentId, Guid? SupplierId,
    Guid SkuId, SkuSnapshot Sku, string LotNumber, DateOnly? ManufacturedOn, DateOnly? ExpiresOn, string? OriginCountryCode,
    DateTimeOffset ReceivedOn, decimal ReceivedQuantity, decimal PurchaseUnitCostAmount, string PurchaseUnitCostCurrency,
    decimal PurchaseUnitCostBase, BatchStatus InitialStatus, InventoryActor Actor);

public sealed record BatchRecallDto(string RecallNumber, string Reason, string CustomerAdvice, DateTimeOffset DeclaredOn, InventoryActor Actor);

public sealed record GoodsReceiptLineDto(
    Guid LineId, Guid SkuId, Guid? PurchaseOrderLineId, string LotNumber, DateOnly? ManufacturedOn, DateOnly? ExpiresOn,
    decimal QuantityAccepted, decimal QuantityRejected, RejectionReason? RejectionReason, string? RejectionNote, Guid LocationId,
    bool HoldForInspection, decimal? UnitCostAmount, string? UnitCostCurrency, decimal? ExchangeRateToBase, Guid? BatchId);

public sealed record GoodsReceiptPostDto(
    string Number, Guid BranchId, Guid? SupplierId, Guid? PurchaseOrderId, Guid? ConsignmentId, string? DeliveryReference,
    DateTimeOffset ReceivedOn, IReadOnlyList<GoodsReceiptLineDto> Lines, InventoryActor Actor);

/// <summary>One violation found while validating a receipt; <see cref="PropertyName"/> is e.g. <c>Lines[2].ExpiresOn</c> (§11.4 C8).</summary>
public sealed record GoodsReceiptViolation(string Code, string Message, string PropertyName);

public sealed record StockLocationFact(Guid LocationId, Guid BranchId, string Code, StockLocationKind Kind, bool IsActive);

public sealed record PurchaseOrderLineFact(Guid LineId, Guid SkuId, decimal OrderedQuantity, decimal PreviouslyAccepted, decimal UnitCostAmount, string CurrencyCode);

/// <summary>Everything a receipt's validation needs that the receipt itself does not carry -- read by the handler,
/// decided by <c>GoodsReceiptAggregate.Validate</c> (rule 20: the invariants live in the aggregate).</summary>
public sealed record GoodsReceiptValidationContext(
    DateOnly BranchToday,
    string BaseCurrency,
    IReadOnlyDictionary<Guid, SkuStockProfile> Skus,
    IReadOnlyDictionary<Guid, StockLocationFact> Locations,
    IReadOnlyDictionary<Guid, PurchaseOrderLineFact> PurchaseOrderLines,
    bool HasConsignment,
    IReadOnlyDictionary<string, decimal> ConsignmentRates);

/// <summary>A movement the Inventory context's own handlers want on the ledger (§8.8, <c>IInventoryLedgerWriter</c>).</summary>
public sealed record StockMovementDraft(
    Guid BranchId, Guid LocationId, Guid SkuId, Guid BatchId, MovementKind Kind, decimal QuantityDelta,
    StockReferenceKind ReferenceKind, Guid ReferenceId, Guid? ReferenceLineId, string? ReferenceLabel, string? Note);

public sealed record StockTransferLineDto(Guid LineId, Guid SkuId, Guid BatchId, decimal Quantity);

public sealed record StockTransferPostDto(
    string Number, Guid BranchId, Guid FromLocationId, Guid ToLocationId, string? Reason, DateTimeOffset TransferredOn,
    IReadOnlyList<StockTransferLineDto> Lines, InventoryActor Actor);

/// <summary>A line of an adjustment request. <see cref="BatchIsExpired"/>/<see cref="BatchStatus"/> are facts the handler read, so the aggregate can
/// decide <c>AdjustmentReasonNotApplicable</c>; <see cref="UnitCostBase"/> prices the line.</summary>
public sealed record AdjustmentLineDto(
    Guid LineId, Guid SkuId, Guid BatchId, decimal QuantityDelta, bool BatchIsExpired, BatchStatus BatchStatus, decimal UnitCostBase);

public sealed record StockAdjustmentRequestDto(
    string Number, Guid BranchId, Guid LocationId, AdjustmentReason? Reason, string? Note, decimal ApprovalThreshold, string BaseCurrency,
    DateTimeOffset RequestedOn, IReadOnlyList<AdjustmentLineDto> Lines, InventoryActor Actor);

public sealed record CountLineDto(Guid SkuId, Guid BatchId, decimal CountedQuantity);

public sealed record StockCountOpenDto(
    string Number, Guid BranchId, Guid LocationId, StockCountScope Scope, IReadOnlyList<Guid> SkuIds, DateTimeOffset OpenedOn, InventoryActor Actor);

/// <summary>The book quantity read at submit for one (SKU, batch); a position never counted becomes a line counted 0.</summary>
public sealed record CountBookDto(Guid SkuId, Guid BatchId, decimal BookQuantity, decimal UnitCostBase);

public sealed record StockCountSubmitDto(
    IReadOnlyList<CountBookDto> Books, bool IsFullLocation, string BaseCurrency, DateTimeOffset SubmittedOn, InventoryActor Actor);

public sealed record ReorderPolicyDto(
    Guid SkuId, Guid LocationId, decimal ReorderPoint, decimal? ReorderQuantity, Guid? PreferredSupplierId, InventoryActor Actor);

public sealed record StockAlertRaiseDto(
    StockAlertKind Kind, Guid BranchId, Guid? SkuId, Guid? BatchId, Guid? LocationId, int? ThresholdDays, decimal? QuantityAtRaise,
    StockReferenceKind? ReferenceKind, Guid? ReferenceId, string? DedupKey, DateTimeOffset RaisedOn, InventoryActor Actor);

public sealed record StorageBreachReportDto(
    Guid LocationId, Guid BranchId, DateTimeOffset ObservedOn, string Condition, decimal? TemperatureC, int? DurationMinutes,
    IReadOnlyList<Guid> AffectedBatchIds, DateTimeOffset ReportedOn, InventoryActor Actor);

/// <summary>A candidate batch-at-a-location for FEFO (§8.9).</summary>
public sealed record FefoCandidate(
    Guid BatchId, Guid LocationId, int LocationPriority, DateOnly? ExpiresOn, DateTimeOffset ReceivedOn, BatchStatus Status, decimal Free);

public sealed record FefoAllocationLine(Guid BatchId, Guid LocationId, decimal Quantity);

public sealed record FefoAllocation(IReadOnlyList<FefoAllocationLine> Lines, decimal Shortfall);

/// <summary>A position with everything a plan needs to know about its batch and location.</summary>
public sealed record StockCandidate(
    Guid BatchId, string LotNumber, DateOnly? ExpiresOn, DateTimeOffset ReceivedOn, BatchStatus BatchStatus,
    Guid LocationId, StockLocationKind LocationKind, decimal OnHand, decimal Reserved)
{
    public decimal Free => OnHand - Reserved;
}

/// <summary>A batch's unit cost in base currency and where it comes from (<c>Landed</c> or <c>Purchase</c>) -- <c>v_batch_unit_cost</c>.</summary>
public sealed record BatchUnitCost(decimal UnitCostBase, string Basis);

public sealed record StockPositionRow(Guid SkuId, Guid BatchId, Guid LocationId, decimal OnHand, decimal Reserved);

public sealed record BatchFact(
    Guid BatchId, Guid SkuId, Guid BranchId, string LotNumber, DateOnly? ExpiresOn, DateTimeOffset ReceivedOn, BatchStatus Status);

public sealed record ReservationRemaining(Guid ReferenceLineId, Guid BatchId, Guid LocationId, Guid SkuId, decimal Quantity);

public sealed record ConsignmentBatchFact(Guid BatchId, decimal ReceivedQuantity, decimal PurchaseUnitCostAmount, string PurchaseUnitCostCurrency);

public sealed record PurchaseOrderLineReceipt(Guid PurchaseOrderLineId, decimal Accepted, decimal Rejected);

public sealed record PurchaseOrderReceiptFact(Guid GoodsReceiptId, string Number, DateTimeOffset ReceivedOn);

/// <summary>One reconciliation mismatch (§8.13): a position that differs from the sum of its entries.</summary>
public sealed record PositionMismatch(Guid BatchId, Guid LocationId, decimal OnHand, decimal ExpectedOnHand, decimal Reserved, decimal ExpectedReserved);

/// <summary>A recipient of a recall notice (§8.12).</summary>
public sealed record RecallRecipient(Guid CustomerId, Guid BranchId);
