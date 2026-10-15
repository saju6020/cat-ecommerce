using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-IMP-001 §8.5 -- owned by <c>PurchaseOrderAggregate</c>. One line per SKU. The SKU is snapshotted
/// (code, name, unit) so reads never join the catalogue; the unit cost is in the order's currency.</summary>
public sealed class PurchaseOrderLine
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public string SkuCode { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string StockUnit { get; private set; } = default!;
    public decimal OrderedQuantity { get; private set; }
    public decimal UnitCostAmount { get; private set; }

    private PurchaseOrderLine() { }

    public PurchaseOrderLine(Guid id, Guid skuId, string skuCode, string displayName, string stockUnit, decimal orderedQuantity, decimal unitCostAmount)
    {
        Id = id;
        SkuId = skuId;
        SkuCode = skuCode;
        DisplayName = displayName;
        StockUnit = stockUnit;
        OrderedQuantity = orderedQuantity;
        UnitCostAmount = unitCostAmount;
    }

    public UnitCost UnitCostIn(string currencyCode) => new(UnitCostAmount, currencyCode);
}
