using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C17 -- sets (replaces) one locale's translation.</summary>
public sealed class SetProductTranslationCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required string Locale { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? IngredientList { get; init; }
    public string? FeedingGuide { get; init; }
}

public sealed class RemoveProductTranslationCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required string Locale { get; init; }
}
