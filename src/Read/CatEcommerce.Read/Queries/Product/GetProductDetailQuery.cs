using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

/// <summary>HO-SHP-001 section 12.4/12.6 -- 404 for draft, archived, unlisted or unknown.</summary>
public sealed class GetProductDetailQuery : Query<ProductDetailViewModel?>
{
    public required string Slug { get; init; }
    public string? Locale { get; init; }
}
