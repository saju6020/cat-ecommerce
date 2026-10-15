using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-IMP-001 §8.6 -- owned by <c>GoodsReceiptAggregate</c>; immutable once the receipt is posted.
/// <see cref="BatchId"/> is minted in the controller and is null when nothing was accepted.</summary>
public sealed class GoodsReceiptLine
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public string SkuCode { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string StockUnit { get; private set; } = default!;
    public Guid? PurchaseOrderLineId { get; private set; }
    public string LotNumber { get; private set; } = default!;
    public DateOnly? ManufacturedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public decimal QuantityAccepted { get; private set; }
    public decimal QuantityRejected { get; private set; }
    public RejectionReason? RejectionReason { get; private set; }
    public string? RejectionNote { get; private set; }
    public Guid LocationId { get; private set; }
    public bool HoldForInspection { get; private set; }
    public decimal? UnitCostAmount { get; private set; }
    public string? UnitCostCurrency { get; private set; }
    public decimal? ExchangeRateToBase { get; private set; }
    public Guid? BatchId { get; private set; }

    private GoodsReceiptLine() { }

    public GoodsReceiptLine(
        Guid id, Guid skuId, string skuCode, string displayName, string stockUnit, Guid? purchaseOrderLineId, string lotNumber,
        DateOnly? manufacturedOn, DateOnly? expiresOn, decimal quantityAccepted, decimal quantityRejected, RejectionReason? rejectionReason,
        string? rejectionNote, Guid locationId, bool holdForInspection, decimal? unitCostAmount, string? unitCostCurrency,
        decimal? exchangeRateToBase, Guid? batchId)
    {
        Id = id;
        SkuId = skuId;
        SkuCode = skuCode;
        DisplayName = displayName;
        StockUnit = stockUnit;
        PurchaseOrderLineId = purchaseOrderLineId;
        LotNumber = lotNumber;
        ManufacturedOn = manufacturedOn;
        ExpiresOn = expiresOn;
        QuantityAccepted = quantityAccepted;
        QuantityRejected = quantityRejected;
        RejectionReason = rejectionReason;
        RejectionNote = rejectionNote;
        LocationId = locationId;
        HoldForInspection = holdForInspection;
        UnitCostAmount = unitCostAmount;
        UnitCostCurrency = unitCostCurrency;
        ExchangeRateToBase = exchangeRateToBase;
        BatchId = batchId;
    }
}
