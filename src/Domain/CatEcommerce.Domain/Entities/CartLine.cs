namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-ORD-001 §8.2 -- one line of a cart, owned by <c>CartAggregate</c>. Its <b>key</b> is <c>(SkuId, CatId)</c>: the same prescription food for two
/// cats is two lines, the same ordinary SKU is one. <c>PriceWhenAdded</c> is the price in force when the line was created and never changes
/// afterwards -- the change since then is what checkout reports (US-ORD-02); a cart reserves no stock and is never a price.
/// </summary>
public sealed class CartLine
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid? CatId { get; private set; }
    public int Quantity { get; private set; }
    public decimal PriceWhenAdded { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public DateTimeOffset AddedOn { get; private set; }
    public DateTimeOffset UpdatedOn { get; private set; }

    private CartLine() { }

    public CartLine(Guid id, Guid skuId, Guid? catId, int quantity, decimal priceWhenAdded, string currencyCode, DateTimeOffset now)
    {
        Id = id;
        SkuId = skuId;
        CatId = catId;
        Quantity = quantity;
        PriceWhenAdded = priceWhenAdded;
        CurrencyCode = currencyCode;
        AddedOn = now;
        UpdatedOn = now;
    }

    internal void SetQuantity(int quantity, DateTimeOffset now)
    {
        Quantity = quantity;
        UpdatedOn = now;
    }

    /// <summary>A merge keeps the older line's price-when-added (the price the customer first saw).</summary>
    internal void KeepOlderPrice(CartLine other)
    {
        if (other.AddedOn >= AddedOn) return;

        PriceWhenAdded = other.PriceWhenAdded;
        CurrencyCode = other.CurrencyCode;
        AddedOn = other.AddedOn;
    }

    internal void SetCat(Guid? catId, DateTimeOffset now)
    {
        CatId = catId;
        UpdatedOn = now;
    }

    public bool HasKey(Guid skuId, Guid? catId) => SkuId == skuId && CatId == catId;
}
