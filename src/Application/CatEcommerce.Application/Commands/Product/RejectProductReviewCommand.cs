using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C12.</summary>
public sealed class RejectProductReviewCommand : Command
{
    public required Guid ReviewId { get; init; }
    public required string Reason { get; init; }
    public string? Note { get; init; }
}
