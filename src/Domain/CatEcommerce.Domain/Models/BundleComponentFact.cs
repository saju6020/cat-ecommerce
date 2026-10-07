namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-SHP-001 section 8.10 -- one component SKU fact the handler gathers for
/// <c>ProductAggregate.SetBundleComponents</c> and for the publication checklist. Read from the
/// component SKU own product.
/// </summary>
public sealed record BundleComponentFact(
    Guid ComponentSkuId,
    string ComponentSkuCode,
    bool IsActive,
    bool ProductIsActive,
    bool IsBundleSku,
    bool IsPrescriptionProduct);
