namespace CatEcommerce.Infrastructure.Persistence.Views;

/// <summary>
/// HO-IMP-001 ADR-116 amendment (2026-10-07) -- one row of <c>inventory.v_sku_sellable</c>, the published read contract beside
/// <c>v_sellable_stock</c>, backed by the trigger-kept <c>inventory.sku_sellable_summary</c>: per SKU, branch and batch expiry date the
/// sellable quantity of stock that is not expired in the branch local date (<c>ExpiresOn</c> is null for undated stock). Keyless, read-only.
/// Summed by (SKU, branch) it equals <c>v_sellable_stock.sellable_quantity</c> at every instant.
/// </summary>
public sealed class SkuSellableEntry
{
    public Guid BranchId { get; set; }
    public Guid SkuId { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public decimal SellableQuantity { get; set; }
    public DateTimeOffset LastChangedOn { get; set; }
}
