using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record ProductReviewViewModel(Guid ReviewId, int Rating, string? Title, string Body, string AuthorDisplayName, string SkuLabel, DateTimeOffset SubmittedOn, bool VerifiedPurchase);

/// <summary>HO-SHP-001 section 12.4 -- public, published reviews only.</summary>
public sealed class GetProductReviewsQuery : Query<PagedResult<ProductReviewViewModel>>
{
    public required string Slug { get; init; }
}
