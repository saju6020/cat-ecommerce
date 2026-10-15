namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>HO-SHP-001 section 10.2/12.2. Bound from configuration section "Shop".</summary>
public sealed class ShopOptions
{
    public const string SectionName = "Shop";

    public ShopWorkerOptions Worker { get; set; } = new();
    public ShopCatalogueOptions Catalogue { get; set; } = new();
    public ShopFoodRecommendationOptions FoodRecommendations { get; set; } = new();

    /// <summary>
    /// HO-SHP-001 section 8.7 -- the branch whose <c>inventory.v_sku_sellable</c> rows the storefront shows. Null (the
    /// default) means the seeded main branch (<c>AdministrationSeed.MainBranchId</c>); configuration can name another.
    /// </summary>
    public Guid? StorefrontBranchId { get; set; }

    /// <summary>The branch the storefront reads stock from: the configured one, else the seeded main branch (owner decision 2026-10-07).</summary>
    public Guid EffectiveStorefrontBranchId => StorefrontBranchId ?? CatEcommerce.Shared.Common.Constants.AdministrationSeed.MainBranchId;
}

public sealed class ShopWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 120;
}

/// <summary>Backlog row 20 -- the `food-recommendations` rate limit, per signed-in customer. Read at request
/// time so a test factory can lower it.</summary>
public sealed class ShopFoodRecommendationOptions
{
    public int PermitLimit { get; set; } = 30;

    public int WindowMinutes { get; set; } = 1;
}

public sealed class ShopCatalogueOptions
{
    public int FacetCacheSeconds { get; set; } = 60;

    /// <summary>HO-SHP-001 section 12.3 -- the `catalogue` rate limit (120 per minute per IP) on public
    /// list, search, filters and detail. Read from configuration at request time, not a literal, so a
    /// test factory can lower it (I-E10) or raise it (the 20,000-SKU performance run, I-E11).</summary>
    public int PermitLimit { get; set; } = 120;

    public int WindowMinutes { get; set; } = 1;
}
