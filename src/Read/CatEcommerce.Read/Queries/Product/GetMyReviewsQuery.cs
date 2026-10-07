using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record MyReviewViewModel(Guid ReviewId, Guid ProductId, string ProductName, string SkuLabel, int Rating, string? Title, string Body, string Status, DateTimeOffset SubmittedOn);

/// <summary>HO-SHP-001 section 12.4.</summary>
public sealed class GetMyReviewsQuery : Query<IReadOnlyList<MyReviewViewModel>>
{
    public required Guid CustomerId { get; init; }
}
