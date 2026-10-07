using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 §15 Phase F -- U-F2 (bundle availability), U-F3 (bundle components),
/// U-F4 (related links).</summary>
public sealed class ProductBundleAndRelatedTests
{
    private static readonly Guid TaxClassId = Guid.NewGuid();

    private static ProductAggregate CreateBundle()
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(
            ProductKind.Bundle, "Kitten Starter Kit", "PurrCare", ProductCategory.HealthAndGrooming, null,
            TaxClassId, "kitten-starter-kit", Guid.NewGuid(), "KIT-1", TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    private static ProductAggregate CreateStandard()
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(
            ProductKind.Standard, "Plain Product", "PurrCare", ProductCategory.HealthAndGrooming, null,
            TaxClassId, "plain-product", null, null, TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    private static BundleComponentFact Fact(Guid skuId, string code = "COMP", bool active = true, bool bundle = false, bool prescription = false) =>
        new(skuId, code, active, ProductIsActive: true, IsBundleSku: bundle, IsPrescriptionProduct: prescription);

    private static void SetComponents(ProductAggregate product, IReadOnlyList<BundleComponentInputDto> inputs, params BundleComponentFact[] facts) =>
        product.SetBundleComponents(inputs, facts, TestUserContext.Create(), Guid.NewGuid());

    // ── U-F2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_F2_BundleAvailability_AllComponentsCovered_InStock() =>
        Assert.Equal(AvailabilityStatus.InStock, SkuAvailabilityRules.EvaluateBundle([(2, 5), (1, 1)]));

    [Fact]
    public void U_F2_BundleAvailability_OneComponentSellableZero_OutOfStock() =>
        Assert.Equal(AvailabilityStatus.OutOfStock, SkuAvailabilityRules.EvaluateBundle([(2, 5), (1, 0)]));

    [Fact]
    public void U_F2_BundleAvailability_FloorOfOneOverTwoIsZero_OutOfStock() =>
        Assert.Equal(AvailabilityStatus.OutOfStock, SkuAvailabilityRules.EvaluateBundle([(2, 1), (1, 1)]));

    // ── U-F3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_F3_SetBundleComponents_Valid_ReplacesTheSet()
    {
        var bundle = CreateBundle();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        SetComponents(bundle, [new(Guid.NewGuid(), a, 2), new(Guid.NewGuid(), b, 1)], Fact(a, "A"), Fact(b, "B"));

        Assert.Equal(2, bundle.BundleComponents.Count);
        Assert.Equal(3, bundle.BundleComponents.Sum(c => c.Quantity));
    }

    [Fact]
    public void U_F3_ComponentIsAnotherBundleSku_Refused()
    {
        var bundle = CreateBundle();
        var other = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, [new(Guid.NewGuid(), other, 1)], Fact(other, "OTHER-KIT", bundle: true)));

        Assert.Equal(BusinessViolationCodes.InvalidBundleComponent, ex.ErrorCode);
        Assert.Contains("OTHER-KIT", ex.Message);
    }

    [Fact]
    public void U_F3_ComponentIsAPrescriptionProduct_Refused()
    {
        var bundle = CreateBundle();
        var rx = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, [new(Guid.NewGuid(), rx, 1)], Fact(rx, "RX-1", prescription: true)));

        Assert.Equal(BusinessViolationCodes.InvalidBundleComponent, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_ComponentIsTheBundlesOwnSku_Refused()
    {
        var bundle = CreateBundle();
        var ownSkuId = bundle.Skus.Single().Id;

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, [new(Guid.NewGuid(), ownSkuId, 1)], Fact(ownSkuId, "KIT-1")));

        Assert.Equal(BusinessViolationCodes.InvalidBundleComponent, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_DuplicateComponentSku_Refused()
    {
        var bundle = CreateBundle();
        var a = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, [new(Guid.NewGuid(), a, 1), new(Guid.NewGuid(), a, 1)], Fact(a, "A")));

        Assert.Equal(BusinessViolationCodes.InvalidBundleComponent, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_QuantityTwentyOne_Refused()
    {
        var bundle = CreateBundle();
        var a = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, [new(Guid.NewGuid(), a, BundleRules.MaxComponentQuantity + 1)], Fact(a, "A")));

        Assert.Equal(BusinessViolationCodes.InvalidBundleComponent, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_DiscontinuedComponent_Refused()
    {
        var bundle = CreateBundle();
        var a = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, [new(Guid.NewGuid(), a, 1)], Fact(a, "OLD-1", active: false)));

        Assert.Equal(BusinessViolationCodes.InvalidBundleComponent, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_MoreThanTwentyComponents_Refused()
    {
        var bundle = CreateBundle();
        var ids = Enumerable.Range(0, BundleRules.MaxComponents + 1).Select(_ => Guid.NewGuid()).ToList();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, ids.Select(id => new BundleComponentInputDto(Guid.NewGuid(), id, 1)).ToList(), ids.Select(id => Fact(id)).ToArray()));

        Assert.Equal(BusinessViolationCodes.BundleComponentLimitReached, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_StandardProduct_CannotHaveComponents()
    {
        var product = CreateStandard();
        var a = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(product, [new(Guid.NewGuid(), a, 1)], Fact(a, "A")));

        Assert.Equal(BusinessViolationCodes.InvalidBundleComponent, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_OneUnitInTotal_IsAcceptedWhileDraftButBlocksPublication()
    {
        var bundle = CreateBundle();
        var a = Guid.NewGuid();
        SetComponents(bundle, [new(Guid.NewGuid(), a, 1)], Fact(a, "A"));

        var facts = new PublicationFacts([bundle.Skus.Single().Id], TaxClassActive: true, [Fact(a, "A")]);
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            bundle.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.ProductNotPublishable, ex.ErrorCode);
    }

    [Fact]
    public void U_F3_TwoUnits_PublishesWhenEveryComponentIsOnSale()
    {
        var bundle = CreateBundle();
        var a = Guid.NewGuid();
        SetComponents(bundle, [new(Guid.NewGuid(), a, 2)], Fact(a, "A"));

        var facts = new PublicationFacts([bundle.Skus.Single().Id], TaxClassActive: true, [Fact(a, "A")]);
        bundle.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(ProductStatus.Active, bundle.Status);
    }

    [Fact]
    public void U_F3_ActiveBundle_CannotBeEditedBelowTheMinimumUnits()
    {
        var bundle = CreateBundle();
        var a = Guid.NewGuid();
        SetComponents(bundle, [new(Guid.NewGuid(), a, 2)], Fact(a, "A"));
        bundle.Publish(new PublicationFacts([bundle.Skus.Single().Id], true, [Fact(a, "A")]), DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            SetComponents(bundle, [new(Guid.NewGuid(), a, 1)], Fact(a, "A")));

        Assert.Equal(BusinessViolationCodes.ProductNotPublishable, ex.ErrorCode);
    }

    // ── U-F4 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_F4_SetRelatedProducts_Valid_Replaces()
    {
        var product = CreateStandard();
        var other = Guid.NewGuid();

        product.SetRelatedProducts([new(Guid.NewGuid(), other, RelatedProductKind.FrequentlyBoughtTogether, 0)], TestUserContext.Create(), Guid.NewGuid());

        Assert.Single(product.RelatedProducts);
    }

    [Fact]
    public void U_F4_SelfLink_Refused()
    {
        var product = CreateStandard();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.SetRelatedProducts([new(Guid.NewGuid(), product.Id, RelatedProductKind.UpSell, 0)], TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.RelatedProductInvalid, ex.ErrorCode);
    }

    [Fact]
    public void U_F4_DuplicateLinkOfTheSameKind_Refused()
    {
        var product = CreateStandard();
        var other = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.SetRelatedProducts(
            [new(Guid.NewGuid(), other, RelatedProductKind.UpSell, 0), new(Guid.NewGuid(), other, RelatedProductKind.UpSell, 1)],
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.RelatedProductInvalid, ex.ErrorCode);
    }

    [Fact]
    public void U_F4_SameProductAsBothKinds_IsAllowed()
    {
        var product = CreateStandard();
        var other = Guid.NewGuid();

        product.SetRelatedProducts(
            [new(Guid.NewGuid(), other, RelatedProductKind.UpSell, 0), new(Guid.NewGuid(), other, RelatedProductKind.FrequentlyBoughtTogether, 0)],
            TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(2, product.RelatedProducts.Count);
    }

    [Fact]
    public void U_F4_ThirteenLinksOfOneKind_Refused()
    {
        var product = CreateStandard();
        var links = Enumerable.Range(0, RelatedProductRules.MaxPerKind + 1)
            .Select(i => new RelatedProductInputDto(Guid.NewGuid(), Guid.NewGuid(), RelatedProductKind.FrequentlyBoughtTogether, i)).ToList();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.SetRelatedProducts(links, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.RelatedProductInvalid, ex.ErrorCode);
    }
}
