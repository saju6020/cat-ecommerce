namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.3.3 UpdateSku / section 11.1 C4. Code and barcode are immutable
/// after creation (ADR-079); only the variant axes may change.</summary>
public sealed record SkuUpdateDto(
    Guid SkuId,
    string? SizeOrWeight,
    string? Flavour,
    string? Colour,
    int? PackQuantity,
    int? ShippingWeightGrams = null);
