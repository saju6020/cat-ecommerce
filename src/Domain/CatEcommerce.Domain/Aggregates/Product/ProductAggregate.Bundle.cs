using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>HO-SHP-001 section 8.10 -- bundle composition and curated related products. Facts
/// about other products (is this SKU a bundle, a prescription product, discontinued) are gathered
/// by the handler and passed in; this aggregate decides (rule 20).</summary>
public sealed partial class ProductAggregate
{
    public void SetBundleComponents(
        IReadOnlyList<BundleComponentInputDto> components, IReadOnlyList<BundleComponentFact> facts,
        UserContext userContext, Guid correlationId)
    {
        if (Kind != ProductKind.Bundle)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent, "Only a bundle product has components.", nameof(components));

        if (components.Count > BundleRules.MaxComponents)
            throw new DomainRuleViolationException(BusinessViolationCodes.BundleComponentLimitReached, $"A bundle may have at most {BundleRules.MaxComponents} components.", nameof(components));

        var ownSkuIds = _skus.Select(s => s.Id).ToHashSet();
        var factsBySku = facts.ToDictionary(f => f.ComponentSkuId);
        var seen = new HashSet<Guid>();

        foreach (var component in components)
        {
            if (component.Quantity < 1 || component.Quantity > BundleRules.MaxComponentQuantity)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent,
                    $"A component quantity must be between 1 and {BundleRules.MaxComponentQuantity}.", nameof(components));

            if (ownSkuIds.Contains(component.ComponentSkuId))
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent, "A bundle cannot contain its own SKU.", nameof(components));

            if (!seen.Add(component.ComponentSkuId))
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent, "A component SKU can appear only once.", nameof(components));

            if (!factsBySku.TryGetValue(component.ComponentSkuId, out var fact))
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent, $"SKU '{component.ComponentSkuId}' was not found.", nameof(components));

            if (fact.IsBundleSku)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent, $"SKU {fact.ComponentSkuCode} is a bundle and cannot be a component.", nameof(components));

            if (fact.IsPrescriptionProduct)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent, $"SKU {fact.ComponentSkuCode} is a prescription product and cannot be bundled.", nameof(components));

            if (!fact.IsActive)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBundleComponent, $"SKU {fact.ComponentSkuCode} is discontinued.", nameof(components));
        }

        // While Active, the composition is a live publication rule (section 8.4): an edit must not
        // leave the bundle below the minimum unit count.
        if (Status == ProductStatus.Active && components.Sum(c => c.Quantity) < BundleRules.MinComponentUnits)
            throw new DomainRuleViolationException(BusinessViolationCodes.ProductNotPublishable,
                $"An active bundle needs at least {BundleRules.MinComponentUnits} component units.", nameof(components));

        _bundleComponents.Clear();
        _bundleComponents.AddRange(components.Select(c => new BundleComponent(c.ComponentId, c.ComponentSkuId, c.Quantity)));

        AddDomainEvent(new BundleCompositionChangedEvent { ProductId = Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void SetRelatedProducts(IReadOnlyList<RelatedProductInputDto> links, UserContext userContext, Guid correlationId)
    {
        var seen = new HashSet<(Guid, RelatedProductKind)>();
        foreach (var link in links)
        {
            if (link.RelatedProductId == Id)
                throw new DomainRuleViolationException(BusinessViolationCodes.RelatedProductInvalid, "A product cannot be related to itself.", nameof(links));

            if (!seen.Add((link.RelatedProductId, link.Kind)))
                throw new DomainRuleViolationException(BusinessViolationCodes.RelatedProductInvalid, "A product can be linked only once per kind.", nameof(links));
        }

        foreach (var group in links.GroupBy(l => l.Kind))
        {
            if (group.Count() > RelatedProductRules.MaxPerKind)
                throw new DomainRuleViolationException(BusinessViolationCodes.RelatedProductInvalid,
                    $"At most {RelatedProductRules.MaxPerKind} {group.Key.DisplayName} links are allowed.", nameof(links));
        }

        _relatedProducts.Clear();
        _relatedProducts.AddRange(links.Select(l => new RelatedProductLink(l.LinkId, l.RelatedProductId, l.Kind, l.SortOrder)));

        AddDomainEvent(new RelatedProductsChangedEvent { ProductId = Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }
}
