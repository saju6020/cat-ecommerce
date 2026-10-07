using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

public sealed class ProductImageInput
{
    public required Guid ImageId { get; init; }
    public Guid? SkuId { get; init; }
    public required Guid FileId { get; init; }
    public required string AltText { get; init; }
    public int SortOrder { get; init; }
    public bool IsPrimary { get; init; }
}

/// <summary>HO-SHP-001 section 11.1 C5.</summary>
public sealed class SetProductImagesCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<ProductImageInput> Images { get; init; }
}
