using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

public sealed class BundleComponentInput
{
    public required Guid ComponentSkuId { get; init; }
    public required int Quantity { get; init; }
}

/// <summary>HO-SHP-001 section 11.1 C13 -- replaces the whole component set of a bundle.</summary>
public sealed class SetBundleComponentsCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<BundleComponentInput> Components { get; init; }
}
