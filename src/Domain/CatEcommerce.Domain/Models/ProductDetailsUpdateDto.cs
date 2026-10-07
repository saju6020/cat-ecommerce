using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 §8.3.3 <c>UpdateDetails</c> / §11.1 C2.</summary>
public sealed record ProductDetailsUpdateDto(
    string Name,
    string Brand,
    string Description,
    ProductCategory Category,
    ProductSubcategory? Subcategory,
    IReadOnlyList<string> Tags,
    Guid TaxClassId,
    string? CountryOfOriginCode,
    bool ListedOnStorefront,
    bool IsExpiryTracked,
    IReadOnlyList<HealthFocus> HealthFocus,
    UserContext UserContext,
    Guid CorrelationId);
