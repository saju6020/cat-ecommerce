namespace CatEcommerce.Infrastructure.Persistence.Views;

/// <summary>
/// HO-SHP-001 section 8.8/12.1 -- maps the migration-created, read-only view
/// <c>product.storefront_catalogue_entry</c> (<c>ToView</c>, keyless, section 12.1). One row per
/// Active, listed product. Never migrated from the read side.
///
/// <c>IsOnSale</c> (column <c>is_on_sale</c>, formerly <c>in_stock</c>) is computed here as "has an Active SKU with a current price" -- see
/// HO-SHP-001.md section 1 Amended (2026-09-28): HO-IMP-001's <c>inventory.v_sellable_stock</c>
/// does not exist in this worktree, so the view cannot join it yet. This is a documented,
/// temporary approximation for the availability filter/sort only; every other column (price,
/// rating, popularity, search text) is the real, final computation.
///
/// Lives in Infrastructure, not Read.ViewModels: <c>Read</c> references <c>Infrastructure</c> for
/// the DbContext, never the reverse, and this type is a <c>DbSet&lt;T&gt;</c> property on
/// <c>CatEcommerceDbContext</c> itself (section 13 names the main DbContext file for the three
/// view entity sets). Query handlers project this into the actual wire-shaped ViewModel.
/// </summary>
public sealed class StorefrontCatalogueEntry
{
    public Guid ProductId { get; init; }
    public string Slug { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Brand { get; init; } = default!;
    public int Category { get; init; }
    public int? Subcategory { get; init; }
    public int Kind { get; init; }
    public string? CountryOfOriginCode { get; init; }
    public int? LifeStage { get; init; }
    public int? ProteinSource { get; init; }
    public bool PrescriptionRequired { get; init; }
    public string SearchText { get; init; } = default!;
    public DateTimeOffset? FirstPublishedOn { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? CurrencyCode { get; init; }
    /// <summary>"On sale": has an Active SKU with a price effective now (a bundle: its own SKU is on sale and every component is Active, listed and priced). It says NOTHING about stock -- renamed from <c>in_stock</c> in HO-SHP-001-FU2, when the interim price-derived stand-in for stock was replaced by <c>inventory.v_sku_sellable</c>; availability is read from Inventory.</summary>
    public bool IsOnSale { get; init; }
    public decimal? RatingAverage { get; init; }
    public int RatingCount { get; init; }
    public int UnitsSold90d { get; init; }
    public Guid? PrimaryImageFileId { get; init; }
    public string? PrimaryImageAltText { get; init; }
}
