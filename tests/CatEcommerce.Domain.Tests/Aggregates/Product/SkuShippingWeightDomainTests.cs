using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001-FU3 -- optional per-SKU shipping weight (owner decision 2026-10-09): null allowed,
/// otherwise a whole number of grams within [Min, Max]; settable on add and on update.</summary>
public sealed class SkuShippingWeightDomainTests
{
    private static ProductAggregate NewProduct()
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(
            ProductKind.Standard, "Weighed Product", "Acme", ProductCategory.HealthAndGrooming, null,
            Guid.NewGuid(), "weighed-product", null, null, TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    [Fact]
    public void AddSku_WithoutWeight_LeavesItNull()
    {
        var product = NewProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "W-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        Assert.Null(product.Skus.Single().ShippingWeightGrams);
    }

    [Theory]
    [InlineData(ProductRules.MinShippingWeightGrams)]
    [InlineData(2500)]
    [InlineData(ProductRules.MaxShippingWeightGrams)]
    public void AddSku_WithValidWeight_StoresIt(int grams)
    {
        var product = NewProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "W-1", null, null, null, null, null, grams), TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(grams, product.Skus.Single().ShippingWeightGrams);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(ProductRules.MaxShippingWeightGrams + 1)]
    public void AddSku_WithOutOfRangeWeight_IsRefused(int grams)
    {
        var product = NewProduct();
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.AddSku(new SkuAddDto(Guid.NewGuid(), "W-1", null, null, null, null, null, grams), TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.InvalidShippingWeight, ex.ErrorCode);
        Assert.Empty(product.Skus);
    }

    [Fact]
    public void UpdateSku_SetsChangesAndClearsTheWeight_AndRefusesInvalid()
    {
        var product = NewProduct();
        var skuId = Guid.NewGuid();
        product.AddSku(new SkuAddDto(skuId, "W-1", null, null, null, null, null, 400), TestUserContext.Create(), Guid.NewGuid());

        product.UpdateSku(new SkuUpdateDto(skuId, null, null, null, null, 1200), TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(1200, product.Skus.Single().ShippingWeightGrams);

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.UpdateSku(new SkuUpdateDto(skuId, null, null, null, null, 0), TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.InvalidShippingWeight, ex.ErrorCode);
        Assert.Equal(1200, product.Skus.Single().ShippingWeightGrams);

        product.UpdateSku(new SkuUpdateDto(skuId, null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        Assert.Null(product.Skus.Single().ShippingWeightGrams);
    }
}
