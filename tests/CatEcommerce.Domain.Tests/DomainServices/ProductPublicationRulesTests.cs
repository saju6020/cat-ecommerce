using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-SHP-001 §15 U-A7, U-A14.</summary>
public sealed class ProductPublicationRulesTests
{
    private static ProductAggregate NonFoodProductWithOneSku()
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scratch Post", "Acme", ProductCategory.ToysAndFurniture,
            null, Guid.NewGuid(), "scratch-post", null, null, TestUserContext.Create(), Guid.NewGuid()));
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "SP-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        return product;
    }

    [Fact]
    public void Evaluate_NoActiveSku_ActiveSkuItemBlockingAndUnmet()
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scratch Post", "Acme", ProductCategory.ToysAndFurniture,
            null, Guid.NewGuid(), "scratch-post", null, null, TestUserContext.Create(), Guid.NewGuid()));

        var checklist = ProductPublicationRules.Evaluate(product, new PublicationFacts([], true, []));

        var activeSkuItem = checklist.Single(i => i.Item == ProductPublicationRules.ActiveSku);
        Assert.True(activeSkuItem.IsBlocking);
        Assert.False(activeSkuItem.IsMet);
    }

    [Fact]
    public void Evaluate_UnpricedActiveSku_PricedSkusItemNamesSkuCode()
    {
        var product = NonFoodProductWithOneSku();

        var checklist = ProductPublicationRules.Evaluate(product, new PublicationFacts([], true, []));

        var pricedItem = checklist.Single(i => i.Item == ProductPublicationRules.PricedSkus);
        Assert.False(pricedItem.IsMet);
        Assert.Contains("SP-1", pricedItem.Message);
    }

    [Fact]
    public void Evaluate_CompleteNonFoodProduct_EveryBlockingItemMet_PrimaryImageWarningOnlyWhenNoImage()
    {
        var product = NonFoodProductWithOneSku();
        var skuId = product.Skus.Single().Id;

        var checklist = ProductPublicationRules.Evaluate(product, new PublicationFacts([skuId], true, []));

        foreach (var item in checklist.Where(i => i.IsBlocking))
            Assert.True(item.IsMet, $"{item.Item} should be met: {item.Message}");

        var primaryImageItem = checklist.Single(i => i.Item == ProductPublicationRules.PrimaryImage);
        Assert.False(primaryImageItem.IsBlocking);
        Assert.False(primaryImageItem.IsMet);
    }
}
