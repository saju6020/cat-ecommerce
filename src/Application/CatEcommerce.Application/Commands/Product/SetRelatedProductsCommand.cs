using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

public sealed class RelatedProductInput
{
    public required Guid RelatedProductId { get; init; }
    /// <summary>"FrequentlyBoughtTogether" or "UpSell".</summary>
    public required string Kind { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>HO-SHP-001 section 11.1 C13 -- replaces the whole curated set.</summary>
public sealed class SetRelatedProductsCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<RelatedProductInput> Links { get; init; }
}
