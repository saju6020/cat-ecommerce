using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record ReviewModerationItemViewModel(
    Guid ReviewId, Guid ProductId, string ProductName, string SkuLabel, int Rating, string? Title, string Body,
    string CustomerDisplayName, DateTimeOffset SubmittedOn, string Status);

/// <summary>HO-SHP-001 section 12.4.</summary>
public sealed class GetReviewModerationQueueQuery : Query<PagedResult<ReviewModerationItemViewModel>>
{
    public string? Status { get; init; }
    public Guid? ProductId { get; init; }
}
