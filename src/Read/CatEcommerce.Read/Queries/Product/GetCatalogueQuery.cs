using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

/// <summary>HO-SHP-001 section 8.8/12.4 -- every filter combines with every sort; filtering,
/// sorting and paging are SQL only.</summary>
public sealed class GetCatalogueQuery : Query<CatalogueResult>
{
    public string? Q { get; init; }
    public string? Category { get; init; }
    public string? Subcategory { get; init; }
    public IReadOnlyList<string> Brand { get; init; } = [];
    public IReadOnlyList<string> Origin { get; init; } = [];
    public IReadOnlyList<string> LifeStage { get; init; } = [];
    public IReadOnlyList<string> Protein { get; init; } = [];
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool? AvailabilityInStock { get; init; }
    public int? MinRating { get; init; }
    public string? Sort { get; init; }
    public string? Locale { get; init; }
}
