using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C11.</summary>
public sealed class EditProductReviewCommand : Command
{
    public required Guid ReviewId { get; init; }
    public required int Rating { get; init; }
    public string? Title { get; init; }
    public required string Body { get; init; }
}
