using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.10 -- one requested bundle component, before the aggregate
/// validates it against <see cref="BundleComponentFact"/>.</summary>
public sealed record BundleComponentInputDto(Guid ComponentId, Guid ComponentSkuId, int Quantity);

/// <summary>HO-SHP-001 section 8.10 -- one requested curated link.</summary>
public sealed record RelatedProductInputDto(Guid LinkId, Guid RelatedProductId, RelatedProductKind Kind, int SortOrder);

/// <summary>HO-SHP-001 section 8.14 -- one locale's translated text.</summary>
public sealed record ProductTranslationInputDto(string Locale, string? Name, string? Description, string? IngredientList, string? FeedingGuide);
