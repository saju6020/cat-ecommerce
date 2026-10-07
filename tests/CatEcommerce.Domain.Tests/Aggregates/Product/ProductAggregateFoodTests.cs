using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 §15 Phase B -- U-B1, U-B3, U-B4, U-B5, U-B6, U-B7, U-B8.</summary>
public sealed class ProductAggregateFoodTests
{
    private static readonly Guid TaxClassId = Guid.NewGuid();

    private static ProductAggregate CreateProduct(ProductCategory category, ProductSubcategory? subcategory = null, string slug = "product")
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Test Product", "Acme", category, subcategory,
            TaxClassId, slug, null, null, TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    private static ProductDetailsUpdateDto DetailsDto(ProductAggregate product, string? countryCode, bool isExpiryTracked = false, IReadOnlyList<HealthFocus>? healthFocus = null) =>
        new(product.Name, product.Brand, product.Description, product.Category, product.Subcategory, product.Tags,
            product.TaxClassId, countryCode, product.ListedOnStorefront, isExpiryTracked, healthFocus ?? [], TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void SetFoodDetails_OnNonFoodProduct_ThrowsFoodDetailsOnlyForFood()
    {
        var product = CreateProduct(ProductCategory.Litter, ProductSubcategory.Clumping);
        var details = new FoodDetails(null, null, null, null, null, null, null);

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.SetFoodDetails(details, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.FoodDetailsOnlyForFood, ex.ErrorCode);
    }

    [Fact]
    public void Publish_FoodProductMissingCountryOfOrigin_MessageMentionsCountryOfOrigin()
    {
        var product = CreateProduct(ProductCategory.CatFood);
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.SetFoodDetails(new FoodDetails("Royal Canin", LifeStage.Kitten, ProteinSource.Chicken, "Chicken", null, null, null), TestUserContext.Create(), Guid.NewGuid());

        var facts = new PublicationFacts([product.Skus.Single().Id], true, []);
        var ex = Assert.Throws<DomainRuleViolationException>(() => product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Contains("country of origin", ex.Message.ToLowerInvariant());

        // Add it -> now publishable (assuming life stage and priced SKU are present).
        product.UpdateDetails(DetailsDto(product, "DE"));
        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(CatEcommerce.Shared.Common.Enums.ProductStatus.Active, product.Status);
    }

    [Fact]
    public void UpdateDetails_ClearCountryOfOriginOnActiveFoodProduct_Throws()
    {
        var product = CreateProduct(ProductCategory.CatFood);
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.SetFoodDetails(new FoodDetails(null, LifeStage.Kitten, null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.UpdateDetails(DetailsDto(product, "DE"));
        var facts = new PublicationFacts([product.Skus.Single().Id], true, []);
        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.UpdateDetails(DetailsDto(product, null)));
        Assert.Equal(BusinessViolationCodes.CountryOfOriginRequired, ex.ErrorCode);
    }

    [Fact]
    public void SetPrescriptionRequired_OnFood_RaisesEvent_OnNonFood_Throws()
    {
        var food = CreateProduct(ProductCategory.CatFood, slug: "food-a");
        food.SetPrescriptionRequired(true, TestUserContext.Create(), Guid.NewGuid());
        Assert.True(food.PrescriptionRequired);

        var litter = CreateProduct(ProductCategory.Litter, ProductSubcategory.Clumping, slug: "litter-a");
        var ex = Assert.Throws<DomainRuleViolationException>(() => litter.SetPrescriptionRequired(true, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.PrescriptionOnlyForFood, ex.ErrorCode);
    }

    [Fact]
    public void SetPrescriptionRequired_On_WhileAComponentOfABundle_IsRefused_NamingTheBundle_ButOffIsAllowed()
    {
        var food = CreateProduct(ProductCategory.CatFood, slug: "food-in-kit");

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            food.SetPrescriptionRequired(true, TestUserContext.Create(), Guid.NewGuid(), ["Starter Kit"]));
        Assert.Equal(BusinessViolationCodes.PrescriptionProductInBundle, ex.ErrorCode);
        Assert.Contains("Starter Kit", ex.Message);
        Assert.False(food.PrescriptionRequired);

        food.SetPrescriptionRequired(true, TestUserContext.Create(), Guid.NewGuid(), []);
        food.SetPrescriptionRequired(false, TestUserContext.Create(), Guid.NewGuid(), ["Starter Kit"]);
        Assert.False(food.PrescriptionRequired);
    }

    [Theory]
    [InlineData("DE", "DE")]
    [InlineData("de", "DE")]
    public void UpdateDetails_CountryCodeNormalised(string input, string expected)
    {
        var product = CreateProduct(ProductCategory.CatFood);
        product.UpdateDetails(DetailsDto(product, input));
        Assert.Equal(expected, product.CountryOfOriginCode);
    }

    [Fact]
    public void UpdateDetails_UnknownCountryCode_ThrowsInvalidCountryCode()
    {
        var product = CreateProduct(ProductCategory.CatFood);
        var ex = Assert.Throws<DomainRuleViolationException>(() => product.UpdateDetails(DetailsDto(product, "XX")));
        Assert.Equal(BusinessViolationCodes.InvalidCountryCode, ex.ErrorCode);
    }

    [Fact]
    public void UpdateDetails_FoodProductWithExpiryTrackingFalseRequested_StoresTrue()
    {
        var product = CreateProduct(ProductCategory.CatFood);
        product.UpdateDetails(DetailsDto(product, "DE", isExpiryTracked: false));
        Assert.True(product.IsExpiryTracked);
    }

    [Fact]
    public void PublicationChecklist_FoodMissingIngredientsAndAnalysis_LabellingWarningOnly_StillPublishable()
    {
        var product = CreateProduct(ProductCategory.CatFood);
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.SetFoodDetails(new FoodDetails("Royal Canin", LifeStage.Kitten, null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.UpdateDetails(DetailsDto(product, "DE"));

        var facts = new PublicationFacts([product.Skus.Single().Id], true, []);
        var checklist = CatEcommerce.Domain.DomainServices.ProductPublicationRules.Evaluate(product, facts);

        var labelling = checklist.Single(i => i.Item == CatEcommerce.Domain.DomainServices.ProductPublicationRules.LabellingComplete);
        Assert.False(labelling.IsBlocking);
        Assert.False(labelling.IsMet);

        // Publish should still succeed since LabellingComplete is warning-only.
        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(CatEcommerce.Shared.Common.Enums.ProductStatus.Active, product.Status);
    }
}
