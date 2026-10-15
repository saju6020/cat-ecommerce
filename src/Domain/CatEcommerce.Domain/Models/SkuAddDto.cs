namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.3.3 AddSku / section 11.1 C4.</summary>
public sealed record SkuAddDto(
    Guid SkuId,
    string Code,
    string? Barcode,
    string? SizeOrWeight,
    string? Flavour,
    string? Colour,
    int? PackQuantity,
    int? ShippingWeightGrams = null);
