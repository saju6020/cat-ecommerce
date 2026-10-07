namespace CatEcommerce.Api.RateLimiting;

/// <summary>HO-SHP-001 section 12.3/12.7. Kept in its own file rather than editing another area's
/// (the same precedent HO-NTF-001's <c>NotificationRateLimitPolicies</c> already set).</summary>
public static class ShopRateLimitPolicies
{
    public const string Catalogue = "catalogue";
    public const string Reviews = "reviews";
    public const string StockAlerts = "stock-alerts";
    public const string FoodRecommendations = "food-recommendations";
}
