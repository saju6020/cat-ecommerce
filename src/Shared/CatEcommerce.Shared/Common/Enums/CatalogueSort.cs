namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.8/10.1.</summary>
public sealed class CatalogueSort(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CatalogueSort Relevance = new(1, nameof(Relevance));
    public static readonly CatalogueSort Newest = new(2, nameof(Newest));
    public static readonly CatalogueSort PriceAscending = new(3, nameof(PriceAscending));
    public static readonly CatalogueSort PriceDescending = new(4, nameof(PriceDescending));
    public static readonly CatalogueSort Popularity = new(5, nameof(Popularity));
    public static readonly CatalogueSort Rating = new(6, nameof(Rating));
}
