namespace CatEcommerce.Infrastructure.Persistence.Views;

/// <summary>
/// HO-IMP-001 §8.7 / ADR-116 -- one row of <c>inventory.v_sellable_stock</c>: per SKU per branch, the quantity sellable at retail-selling
/// locations (on-hand minus reserved, over <c>Available</c> batches that are not expired in the branch's local date) and the FEFO-first expiry.
/// A keyless, read-only view created by a migration; the published read contract for HO-SHP-001 and HO-ORD-001 (<c>GetSellableStockQuery</c>).
/// </summary>
public sealed class SellableStockEntry
{
    public Guid BranchId { get; set; }
    public Guid SkuId { get; set; }
    public decimal SellableQuantity { get; set; }
    public DateOnly? NextExpiresOn { get; set; }
}
