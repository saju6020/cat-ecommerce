using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C1.</summary>
public sealed class CreateProductCommand : Command
{
    public required Guid ProductId { get; init; }
    public required string Kind { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string Category { get; init; }
    public string? Subcategory { get; init; }
    public required Guid TaxClassId { get; init; }
    public required string Slug { get; init; }
    public Guid? BundleSkuId { get; init; }
    public string? BundleSkuCode { get; init; }
}
