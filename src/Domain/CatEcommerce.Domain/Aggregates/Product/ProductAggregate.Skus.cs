using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>HO-SHP-001 section 8.3.1/8.3.3 -- SKU lifecycle. Code and barcode uniqueness across
/// every product is the handler's repository pre-check plus the unique index (section 11.4 C4);
/// everything checkable from this product's own SKU list lives here.</summary>
public sealed partial class ProductAggregate
{
    public void AddSku(SkuAddDto dto, UserContext userContext, Guid correlationId)
    {
        if (Kind == ProductKind.Bundle)
            throw new DomainRuleViolationException(BusinessViolationCodes.SkuLimitReached, "A bundle has exactly one SKU and cannot have another added.", nameof(dto.Code));

        if (_skus.Count >= ProductRules.MaxSkus)
            throw new DomainRuleViolationException(BusinessViolationCodes.SkuLimitReached, $"A product may have at most {ProductRules.MaxSkus} SKUs.", nameof(dto.Code));

        var sku = new Sku(dto.SkuId, dto.Code, dto.Barcode, dto.SizeOrWeight, dto.Flavour, dto.Colour, dto.PackQuantity, dto.ShippingWeightGrams);

        // Row 32: the PricedSkus rule holds for as long as the product is Active, not only at publish. A new SKU
        // cannot be priced before it exists, so on an Active product it starts inactive (not sold, not shown, not
        // counted by the rule); staff price it, then ReactivateSku -- which requires a price effective now.
        if (Status == ProductStatus.Active) sku.Discontinue();
        _skus.Add(sku);

        ValidateVariantCombinations();
        RefreshSearchText();

        AddDomainEvent(new SkuAddedEvent { ProductId = Id, SkuId = sku.Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void UpdateSku(SkuUpdateDto dto, UserContext userContext, Guid correlationId)
    {
        var sku = FindSkuOrThrow(dto.SkuId);
        sku.UpdateAxes(dto.SizeOrWeight, dto.Flavour, dto.Colour, dto.PackQuantity, dto.ShippingWeightGrams);

        ValidateVariantCombinations();
        RefreshSearchText();

        AddDomainEvent(new SkuUpdatedEvent { ProductId = Id, SkuId = sku.Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void DiscontinueSku(Guid skuId, UserContext userContext, Guid correlationId)
    {
        var sku = FindSkuOrThrow(skuId);

        if (Status == ProductStatus.Active && sku.Status == SkuStatus.Active &&
            _skus.Count(s => s.Status == SkuStatus.Active) <= 1)
        {
            throw new DomainRuleViolationException(BusinessViolationCodes.LastActiveSkuCannotBeDiscontinued,
                "The last active SKU of an active product cannot be discontinued.", nameof(skuId));
        }

        sku.Discontinue();

        AddDomainEvent(new SkuDiscontinuedEvent { ProductId = Id, SkuId = sku.Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <param name="pricedSkuIds">The ids of this product's SKUs that have a price effective now; on an Active
    /// product a SKU is only reactivated if it is one of them (row 32, the PricedSkus rule).</param>
    public void ReactivateSku(Guid skuId, IReadOnlyCollection<Guid> pricedSkuIds, UserContext userContext, Guid correlationId)
    {
        var sku = FindSkuOrThrow(skuId);

        if (Status == ProductStatus.Active && sku.Status != SkuStatus.Active && !pricedSkuIds.Contains(skuId))
            throw new DomainRuleViolationException(BusinessViolationCodes.ProductNotPublishable,
                $"Set a price for SKU {sku.Code} before reactivating it on an active product.", ProductPublicationRules.PricedSkus);

        sku.Reactivate();

        AddDomainEvent(new SkuReactivatedEvent { ProductId = Id, SkuId = sku.Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    private Sku FindSkuOrThrow(Guid skuId) =>
        _skus.FirstOrDefault(s => s.Id == skuId)
        ?? throw new DomainRuleViolationException(BusinessViolationCodes.SkuNotFound, $"SKU '{skuId}' was not found on this product.", nameof(skuId));

    /// <summary>Section 8.3.1: with more than one SKU, every SKU has at least one axis set
    /// (VariantAttributesRequired), and no two SKUs share the same combination, compared
    /// case-insensitively (DuplicateVariant).</summary>
    private void ValidateVariantCombinations()
    {
        if (_skus.Count <= 1) return;

        if (_skus.Any(s => !s.HasAnyAxis))
            throw new DomainRuleViolationException(BusinessViolationCodes.VariantAttributesRequired,
                "With more than one SKU, every SKU must have at least one variant attribute set.", nameof(Skus));

        var duplicate = _skus.GroupBy(s => s.VariantKey).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new DomainRuleViolationException(BusinessViolationCodes.DuplicateVariant,
                "Two SKUs cannot share the same size/weight, flavour, colour and pack quantity combination.", nameof(Skus));
    }
}
