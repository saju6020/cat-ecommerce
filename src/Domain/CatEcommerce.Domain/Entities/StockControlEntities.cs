namespace CatEcommerce.Domain.Entities;

// HO-IMP-001 §8.10 -- the lines of transfers, adjustments and counts.

public sealed class StockTransferLine
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid BatchId { get; private set; }
    public decimal Quantity { get; private set; }

    private StockTransferLine() { }

    public StockTransferLine(Guid id, Guid skuId, Guid batchId, decimal quantity)
    {
        Id = id;
        SkuId = skuId;
        BatchId = batchId;
        Quantity = quantity;
    }
}

public sealed class StockAdjustmentLine
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid BatchId { get; private set; }
    public decimal QuantityDelta { get; private set; }
    public decimal UnitCostBase { get; private set; }

    private StockAdjustmentLine() { }

    public StockAdjustmentLine(Guid id, Guid skuId, Guid batchId, decimal quantityDelta, decimal unitCostBase)
    {
        Id = id;
        SkuId = skuId;
        BatchId = batchId;
        QuantityDelta = quantityDelta;
        UnitCostBase = unitCostBase;
    }
}

/// <summary>A counted (SKU, batch). <see cref="BookQuantity"/> and <see cref="Variance"/> are set at submit and frozen (ADR-108).</summary>
public sealed class StockCountLine
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid BatchId { get; private set; }
    public decimal CountedQuantity { get; private set; }
    public decimal? BookQuantity { get; private set; }
    public decimal? Variance { get; private set; }
    public decimal UnitCostBase { get; private set; }
    public Guid CountedBy { get; private set; }
    public DateTimeOffset CountedOn { get; private set; }

    private StockCountLine() { }

    public StockCountLine(Guid id, Guid skuId, Guid batchId, decimal countedQuantity, Guid countedBy, DateTimeOffset countedOn)
    {
        Id = id;
        SkuId = skuId;
        BatchId = batchId;
        CountedQuantity = countedQuantity;
        CountedBy = countedBy;
        CountedOn = countedOn;
    }

    internal void Recount(decimal countedQuantity, Guid countedBy, DateTimeOffset countedOn)
    {
        CountedQuantity = countedQuantity;
        CountedBy = countedBy;
        CountedOn = countedOn;
    }

    internal void Freeze(decimal bookQuantity, decimal unitCostBase)
    {
        BookQuantity = bookQuantity;
        Variance = CountedQuantity - bookQuantity;
        UnitCostBase = unitCostBase;
    }
}
