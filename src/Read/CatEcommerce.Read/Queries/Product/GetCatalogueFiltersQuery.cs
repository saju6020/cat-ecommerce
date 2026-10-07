using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record NamedCount(string Value, int Count);

/// <summary>HO-SHP-001 section 12.4 -- `brands[{ name, count }]` (the other facets are `{ value, count }`).</summary>
public sealed record BrandCount(string Name, int Count);
public sealed record OriginCount(string Code, string Name, int Count);
public sealed record PriceBounds(decimal? Min, decimal? Max);

public sealed record CatalogueFiltersViewModel(
    IReadOnlyList<BrandCount> Brands,
    IReadOnlyList<OriginCount> Origins,
    IReadOnlyList<NamedCount> LifeStages,
    IReadOnlyList<NamedCount> ProteinSources,
    PriceBounds Price);

/// <summary>HO-SHP-001 section 8.8/12.4 -- facets over active, listed products, optionally within
/// a category.</summary>
public sealed class GetCatalogueFiltersQuery : Query<CatalogueFiltersViewModel>
{
    public string? Category { get; init; }
}
