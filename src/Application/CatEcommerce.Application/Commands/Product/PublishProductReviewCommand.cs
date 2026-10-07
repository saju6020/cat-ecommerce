using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C12.</summary>
public sealed class PublishProductReviewCommand : Command
{
    public required Guid ReviewId { get; init; }
}
