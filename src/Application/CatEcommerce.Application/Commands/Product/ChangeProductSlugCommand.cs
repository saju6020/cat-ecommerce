using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C3.</summary>
public sealed class ChangeProductSlugCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required string Slug { get; init; }
}
