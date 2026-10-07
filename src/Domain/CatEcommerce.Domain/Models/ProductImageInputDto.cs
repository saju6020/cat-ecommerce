namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.3.2 SetImages / section 11.1 C5. The whole set is replaced at
/// once.</summary>
public sealed record ProductImageInputDto(
    Guid ImageId,
    Guid? SkuId,
    Guid FileId,
    string AltText,
    int SortOrder,
    bool IsPrimary);
