using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C4.</summary>
public sealed class UpdateSkuCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required Guid SkuId { get; init; }
    public string? SizeOrWeight { get; init; }
    public string? Flavour { get; init; }
    public string? Colour { get; init; }
    public int? PackQuantity { get; init; }
    public int? ShippingWeightGrams { get; init; }
}
