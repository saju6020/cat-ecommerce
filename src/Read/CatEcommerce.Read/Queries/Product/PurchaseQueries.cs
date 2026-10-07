using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

/// <summary>HO-SHP-001 section 12.4 -- per-SKU purchasability for the signed-in customer. Null when
/// the product is not on the storefront (404).</summary>
public sealed class GetPurchasabilityQuery : Query<PurchasabilityViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required string Slug { get; init; }
}

/// <summary>
/// HO-SHP-001 section 8.9 -- contract query, dispatched in-process by HO-ORD-001 at add-to-cart
/// AND at checkout, so the two checks go through the same gate and cannot drift. Evaluated at the
/// instant of the call: <c>IDietAuthorisationGate</c> computes "today" itself on every call
/// (HO-PHM-001 ADR-149), so there is no <c>AsOf</c> (section 1 Amended, 2026-10-04). Null when the
/// SKU is unknown.
/// </summary>
public sealed class CheckPrescriptionEligibilityQuery : Query<PrescriptionEligibilityViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required Guid SkuId { get; init; }
    public required Guid CatId { get; init; }
}

/// <summary>HO-SHP-001 section 8.10 -- contract query; null for a non-bundle SKU.</summary>
public sealed class GetBundleCompositionQuery : Query<IReadOnlyList<BundleCompositionLineViewModel>?>
{
    public required Guid BundleSkuId { get; init; }
}

/// <summary>HO-SHP-001 section 12.4 -- public, with an optional customer.</summary>
public sealed class GetProductRecommendationsQuery : Query<RecommendationsViewModel?>
{
    public required string Slug { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Locale { get; init; }
}

/// <summary>HO-SHP-001 section 8.12 -- contract query consumed by HO-ORD-001 and POS.</summary>
public sealed class GetSkuSaleFactsQuery : Query<IReadOnlyList<SkuSaleFactViewModel>>
{
    public required IReadOnlyList<Guid> SkuIds { get; init; }
    public required DateTimeOffset AsOf { get; init; }
}

/// <summary>HO-SHP-001 section 8.12 -- contract query consumed by HO-IMP-001 and HO-RPT-001.</summary>
public sealed class GetSkuCatalogueFactsQuery : Query<IReadOnlyList<SkuCatalogueFactViewModel>>
{
    public required IReadOnlyList<Guid> SkuIds { get; init; }
}
