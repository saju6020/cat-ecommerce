using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

/// <summary>HO-SHP-001 section 8.13 / 12.4 -- the cat must be the caller's and alive, else null (404).</summary>
public sealed class GetFoodRecommendationsQuery : Query<FoodRecommendationsViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public string? Locale { get; init; }
}

public sealed class GetLocalesQuery : Query<LocalesViewModel>;
