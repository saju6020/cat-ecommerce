namespace CatEcommerce.Domain.Entities;

/// <summary>HO-SHP-001 section 8.11 -- owned by <c>WishlistAggregate</c>. One per SKU; an item for an
/// archived product or discontinued SKU stays and reads back as not on sale.</summary>
public sealed class WishlistItem
{
    public Guid SkuId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTimeOffset AddedOn { get; private set; }

    private WishlistItem() { }

    public WishlistItem(Guid skuId, Guid productId, DateTimeOffset addedOn)
    {
        SkuId = skuId;
        ProductId = productId;
        AddedOn = addedOn;
    }
}
