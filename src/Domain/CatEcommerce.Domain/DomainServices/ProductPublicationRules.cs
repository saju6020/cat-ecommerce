using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.4 -- pure. Returns the checklist <c>ProductAggregate.Publish</c> enforces
/// and the staff detail read displays, so the screen shows exactly what the server will refuse.
/// </summary>
public static class ProductPublicationRules
{
    public const string ActiveSku = "ActiveSku";
    public const string PricedSkus = "PricedSkus";
    public const string TaxClassActive = "TaxClassActive";
    public const string CountryOfOrigin = "CountryOfOrigin";
    public const string FoodDetailsItem = "FoodDetails";
    public const string PrimaryImage = "PrimaryImage";
    public const string LabellingComplete = "LabellingComplete";
    public const string BundleComposition = "BundleComposition";

    public static IReadOnlyList<PublicationChecklistItem> Evaluate(ProductAggregate product, PublicationFacts facts)
    {
        var items = new List<PublicationChecklistItem>();

        var activeSkus = product.Skus.Where(s => s.Status == SkuStatus.Active).ToList();
        items.Add(new PublicationChecklistItem(ActiveSku, true, activeSkus.Count > 0, "Add at least one active SKU."));

        var unpriced = activeSkus.Where(s => !facts.PricedSkuIds.Contains(s.Id)).ToList();
        items.Add(new PublicationChecklistItem(PricedSkus, true, unpriced.Count == 0,
            unpriced.Count == 0 ? "" : "Set a price for SKU " + string.Join(", ", unpriced.Select(s => s.Code)) + "."));

        items.Add(new PublicationChecklistItem(TaxClassActive, true, facts.TaxClassActive, "The selected tax class is not active."));

        items.Add(new PublicationChecklistItem(CountryOfOrigin, product.IsFood,
            !product.IsFood || product.CountryOfOriginCode is not null,
            "Add the country of origin before publishing this food product."));

        items.Add(new PublicationChecklistItem(FoodDetailsItem, product.IsFood,
            !product.IsFood || product.FoodDetails?.LifeStage is not null,
            "Set the life stage before publishing this food product."));

        items.Add(new PublicationChecklistItem(PrimaryImage, false, product.Images.Count > 0,
            "Products without a photo convert poorly."));

        items.Add(new PublicationChecklistItem(LabellingComplete, false,
            !product.IsFood || (product.FoodDetails?.HasFullLabelling ?? false),
            "Add manufacturer, ingredients, guaranteed analysis, feeding guide and storage instructions for complete labelling."));

        if (product.Kind == ProductKind.Bundle)
        {
            var isMet = EvaluateBundleComposition(product, facts);
            items.Add(new PublicationChecklistItem(BundleComposition, true, isMet,
                "A bundle needs at least two component units, each an active SKU of an active, non-bundle, non-prescription product."));
        }

        return items;
    }

    private static bool EvaluateBundleComposition(ProductAggregate product, PublicationFacts facts)
    {
        if (product.BundleComponents.Count == 0) return false;

        var factsBySkuId = facts.BundleComponentFacts.ToDictionary(f => f.ComponentSkuId);
        var totalUnits = 0;

        foreach (var component in product.BundleComponents)
        {
            if (!factsBySkuId.TryGetValue(component.ComponentSkuId, out var fact)) return false;
            if (!fact.IsActive || !fact.ProductIsActive || fact.IsBundleSku || fact.IsPrescriptionProduct) return false;

            totalUnits += component.Quantity;
        }

        return totalUnits >= Shared.Common.Constants.BundleRules.MinComponentUnits;
    }
}
