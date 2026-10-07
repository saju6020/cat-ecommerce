using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 §11.1 C1. A bundle is created with its own single SKU in the same call
/// (§8.3.3), so the bundle's SKU fields ride along here rather than needing a second command.</summary>
public sealed record ProductCreateDto(
    ProductKind Kind,
    string Name,
    string Brand,
    ProductCategory Category,
    ProductSubcategory? Subcategory,
    Guid TaxClassId,
    string Slug,
    Guid? BundleSkuId,
    string? BundleSkuCode,
    UserContext UserContext,
    Guid CorrelationId);
