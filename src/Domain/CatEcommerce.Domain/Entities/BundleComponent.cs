namespace CatEcommerce.Domain.Entities;

/// <summary>HO-SHP-001 section 8.10 -- owned by a bundle <c>ProductAggregate</c>.</summary>
public sealed class BundleComponent
{
    public Guid Id { get; private set; }
    public Guid ComponentSkuId { get; private set; }
    public int Quantity { get; private set; }

    private BundleComponent() { }

    public BundleComponent(Guid id, Guid componentSkuId, int quantity)
    {
        Id = id;
        ComponentSkuId = componentSkuId;
        Quantity = quantity;
    }
}
