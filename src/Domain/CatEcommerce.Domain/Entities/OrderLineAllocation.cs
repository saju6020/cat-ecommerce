namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-ORD-001 §8.3.1 / §8.9 (D-4, ADR-208) -- which batch, at which location, was reserved for an order line (FEFO, chosen by Inventory -- <b>never</b> by
/// Ordering). For a bundle line, <c>SkuId</c> is the component. The recall trace reads this table by <c>batch_id</c>.
/// </summary>
public sealed class OrderLineAllocation
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid BatchId { get; private set; }
    public string LotNumber { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public decimal Quantity { get; private set; }
    public DateOnly? BatchExpiresOn { get; private set; }

    private OrderLineAllocation() { }

    public OrderLineAllocation(Guid id, Guid skuId, Guid batchId, string lotNumber, Guid locationId, decimal quantity, DateOnly? batchExpiresOn)
    {
        Id = id;
        SkuId = skuId;
        BatchId = batchId;
        LotNumber = lotNumber;
        LocationId = locationId;
        Quantity = quantity;
        BatchExpiresOn = batchExpiresOn;
    }
}
