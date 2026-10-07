namespace CatEcommerce.Infrastructure.Persistence.Views;

/// <summary>Maps <c>product.catalogue_term</c> (section 12.1, keyless, <c>ToView</c>) -- distinct
/// lower-cased brand and product names of active, listed products, for "did you mean" (section 8.8).</summary>
public sealed class CatalogueTermRow
{
    public string Term { get; init; } = default!;
}
