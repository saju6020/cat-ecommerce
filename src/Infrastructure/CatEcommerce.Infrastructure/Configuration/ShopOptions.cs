namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>HO-SHP-001 section 10.2/12.2. Bound from configuration section "Shop".</summary>
public sealed class ShopOptions
{
    public const string SectionName = "Shop";

    public ShopWorkerOptions Worker { get; set; } = new();
    public ShopCatalogueOptions Catalogue { get; set; } = new();
    public ShopFoodRecommendationOptions FoodRecommendations { get; set; } = new();

    /// <summary>
    /// HO-SHP-001 section 4.3 / ADR-082: until HO-IMP-001 supplies stock, the storefront is FAIL-CLOSED --
    /// every SKU reads out of stock and is not purchasable. Setting this true switches on the interim
    /// approximation "an Active SKU on an Active, listed product with a price effective now counts as
    /// available" (HO-SHP-001.md section 1 Amended, 2026-09-28 / 2026-10-04), which only the Testing
    /// environment enables so the catalogue and availability tests stay meaningful. Production default:
    /// false. Removed when the inventory view lands.
    /// </summary>
    public bool InterimAvailabilityFromPrice { get; set; }
}

/// <summary>Backlog row 13: <c>Shop:InterimAvailabilityFromPrice</c> is a stand-in for stock that makes every
/// priced SKU "in stock". It exists so Development and Testing stay meaningful; it must never be on in a
/// deployed environment, where it would sell what the shop does not have. Refused at startup (ValidateOnStart).</summary>
public sealed class ShopOptionsValidator(Microsoft.Extensions.Hosting.IHostEnvironment environment)
    : Microsoft.Extensions.Options.IValidateOptions<ShopOptions>
{
    public Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, ShopOptions options)
    {
        var isDevelopmentOrTesting = environment.EnvironmentName is "Development" or "Testing";
        return options.InterimAvailabilityFromPrice && !isDevelopmentOrTesting
            ? Microsoft.Extensions.Options.ValidateOptionsResult.Fail(
                "Shop:InterimAvailabilityFromPrice must never be true outside Development/Testing (HO-SHP-001 section 4.3).")
            : Microsoft.Extensions.Options.ValidateOptionsResult.Success;
    }
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
