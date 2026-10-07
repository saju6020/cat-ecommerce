using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record PurchasedSkuViewModel(Guid SkuId, string Label);

public sealed record ReviewEligibilityViewModel(bool CanReview, string? Reason, IReadOnlyList<PurchasedSkuViewModel> PurchasedSkus, Guid? ExistingReviewId);

/// <summary>HO-SHP-001 section 12.4.</summary>
public sealed class GetReviewEligibilityQuery : Query<ReviewEligibilityViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required string Slug { get; init; }
}
