using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 §15 Phase A -- U-A1..U-A14 (a representative subset; taxonomy, SKUs,
/// barcodes, images, lifecycle and search text are all exercised).</summary>
public sealed class ProductAggregateTests
{
    private static readonly Guid TaxClassId = Guid.NewGuid();

    // Defaults to a non-food category so generic lifecycle tests are not food-gated (country of
    // origin / life stage) -- food-specific rules have their own dedicated tests (Phase B).
    private static ProductAggregate CreateStandardProduct(string slug = "royal-canin-kitten", ProductCategory? category = null, ProductSubcategory? subcategory = null)
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(
            ProductKind.Standard, "Royal Canin Kitten", "Royal Canin", category ?? ProductCategory.HealthAndGrooming, subcategory,
            TaxClassId, slug, null, null, TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    [Fact]
    public void Create_Valid_SetsFieldsAndBuildsSearchText()
    {
        var product = CreateStandardProduct();

        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal("Royal Canin Kitten", product.Name);
        Assert.Contains("royal canin kitten", product.SearchText);
        Assert.Contains("royal canin", product.SearchText);
        Assert.Single(product.Events);
    }

    [Theory]
    [InlineData("Litter", "Dry")]
    public void Create_SubcategoryNotBelongingToCategory_Throws(string category, string subcategory)
    {
        var cat = Enumeration.FromDisplayName<ProductCategory>(category);
        var sub = Enumeration.FromDisplayName<ProductSubcategory>(subcategory);

        var ex = Assert.Throws<DomainRuleViolationException>(() => CreateStandardProduct(category: cat, subcategory: sub));
        Assert.Equal(BusinessViolationCodes.InvalidProductCategory, ex.ErrorCode);
    }

    [Fact]
    public void Create_EverySubcategoryWithItsOwnParent_Accepted()
    {
        foreach (var subcategory in Enumeration.GetAll<ProductSubcategory>())
        {
            var product = CreateStandardProduct(slug: $"slug-{subcategory.Value}", category: subcategory.Category, subcategory: subcategory);
            Assert.Equal(subcategory, product.Subcategory);
        }
    }

    [Fact]
    public void AddSku_SecondSkuWithNoAxis_ThrowsVariantAttributesRequired()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-K-2KG", null, "2 kg", null, null, null), TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-K-4KG", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.VariantAttributesRequired, ex.ErrorCode);
    }

    [Fact]
    public void AddSku_DuplicateVariantCaseInsensitive_Throws()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-K-2KG-SAL", null, "2 kg", "Salmon", null, null), TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-K-2KG-SAL-2", null, "2 KG", "salmon", null, null), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.DuplicateVariant, ex.ErrorCode);
    }

    [Fact]
    public void AddSku_OneHundredAndFirst_ThrowsSkuLimitReached()
    {
        var product = CreateStandardProduct();
        for (var i = 0; i < 99; i++)
            product.AddSku(new SkuAddDto(Guid.NewGuid(), $"RC-{i}", null, $"axis-{i}", null, null, null), TestUserContext.Create(), Guid.NewGuid());

        // 100th (still under the 1-SKU exemption path since >1 already exists) succeeds.
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-99", null, "axis-99", null, null, null), TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(100, product.Skus.Count);

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-100", null, "axis-100", null, null, null), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.SkuLimitReached, ex.ErrorCode);
    }

    [Theory]
    [InlineData("5901234123457", true)]  // valid EAN-13
    [InlineData("5901234123458", false)] // bad check digit
    [InlineData("ABC-123", true)]        // non-numeric, format only
    [InlineData("12", false)]            // too short
    public void AddSku_BarcodeValidation(string barcode, bool expectValid)
    {
        var product = CreateStandardProduct();

        if (expectValid)
        {
            product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-BC", barcode, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
            Assert.Equal(barcode, product.Skus.Single().Barcode);
        }
        else
        {
            var ex = Assert.Throws<DomainRuleViolationException>(() =>
                product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-BC", barcode, null, null, null, null), TestUserContext.Create(), Guid.NewGuid()));
            Assert.Equal(BusinessViolationCodes.InvalidBarcode, ex.ErrorCode);
        }
    }

    [Fact]
    public void SetImages_NoAltText_ThrowsImageAltTextRequired()
    {
        var product = CreateStandardProduct();
        var images = new[] { new ProductImageInputDto(Guid.NewGuid(), null, Guid.NewGuid(), "", 0, true) };

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.SetImages(images, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.ImageAltTextRequired, ex.ErrorCode);
    }

    [Fact]
    public void SetImages_TwoPrimaries_ThrowsInvalidImage()
    {
        var product = CreateStandardProduct();
        var images = new[]
        {
            new ProductImageInputDto(Guid.NewGuid(), null, Guid.NewGuid(), "Front", 0, true),
            new ProductImageInputDto(Guid.NewGuid(), null, Guid.NewGuid(), "Back", 1, true),
        };

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.SetImages(images, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.InvalidImage, ex.ErrorCode);
    }

    [Fact]
    public void SetImages_SkuIdOfAnotherProduct_ThrowsInvalidImage()
    {
        var product = CreateStandardProduct();
        var images = new[] { new ProductImageInputDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Front", 0, true) };

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.SetImages(images, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.InvalidImage, ex.ErrorCode);
    }

    [Fact]
    public void SetImages_FortyOneImages_ThrowsImageLimitReached()
    {
        var product = CreateStandardProduct();
        var images = Enumerable.Range(0, 41).Select(i => new ProductImageInputDto(Guid.NewGuid(), null, Guid.NewGuid(), $"Image {i}", i, i == 0)).ToList();

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.SetImages(images, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.ImageLimitReached, ex.ErrorCode);
    }

    [Fact]
    public void StatusTransitions_OnlySpecifiedArrowsAllowed()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());

        // Draft -> Unpublish is invalid.
        Assert.Throws<DomainRuleViolationException>(() => product.Unpublish(TestUserContext.Create(), Guid.NewGuid()));
        // Draft -> Archive is invalid.
        Assert.Throws<DomainRuleViolationException>(() => product.Archive(TestUserContext.Create(), Guid.NewGuid()));

        var facts = new PublicationFacts([product.Skus.Single().Id], true, []);
        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(ProductStatus.Active, product.Status);

        // Active -> Publish again is invalid.
        Assert.Throws<DomainRuleViolationException>(() => product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        product.Archive(TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(ProductStatus.Archived, product.Status);

        // Archived -> Unpublish is invalid.
        Assert.Throws<DomainRuleViolationException>(() => product.Unpublish(TestUserContext.Create(), Guid.NewGuid()));

        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public void Publish_NoActiveSku_ThrowsListingActiveSku()
    {
        var product = CreateStandardProduct();
        var facts = new PublicationFacts([], true, []);

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.ProductNotPublishable, ex.ErrorCode);
        Assert.Contains("active SKU", ex.Message);
    }

    [Fact]
    public void Publish_UnpricedActiveSku_MessageNamesSkuCode()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-K-2KG", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        var facts = new PublicationFacts([], true, []);

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Contains("RC-K-2KG", ex.Message);
    }

    [Fact]
    public void ChangeSlug_BeforeAndAfterFirstPublication()
    {
        var product = CreateStandardProduct();
        product.ChangeSlug("new-slug", TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal("new-slug", product.Slug);

        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        var facts = new PublicationFacts([product.Skus.Single().Id], true, []);
        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.ChangeSlug("another-slug", TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.SlugLockedAfterPublication, ex.ErrorCode);
    }

    [Fact]
    public void DiscontinueSku_LastActiveSkuOfActiveProduct_Throws()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        var skuId = product.Skus.Single().Id;
        var facts = new PublicationFacts([skuId], true, []);
        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.DiscontinueSku(skuId, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.LastActiveSkuCannotBeDiscontinued, ex.ErrorCode);
    }

    [Fact]
    public void DiscontinueSku_OnDraftProduct_Allowed()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        var skuId = product.Skus.Single().Id;

        product.DiscontinueSku(skuId, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(SkuStatus.Discontinued, product.Skus.Single().Status);
    }

    [Fact]
    public void UpdateDetails_ChangeFlavourAndTags_RefreshesSearchText()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, "Salmon", null, null), TestUserContext.Create(), Guid.NewGuid());

        Assert.Contains("salmon", product.SearchText);

        product.UpdateDetails(new ProductDetailsUpdateDto(
            "Royal Canin Kitten", "Royal Canin", "", ProductCategory.CatFood, null, ["Grain Free"],
            TaxClassId, null, true, false, [], TestUserContext.Create(), Guid.NewGuid()));

        Assert.Contains("grain-free", product.SearchText);
    }

    [Fact]
    public void UpdateDetails_21Tags_ThrowsTagLimitExceeded_AndNormalisesTag()
    {
        var product = CreateStandardProduct();

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.UpdateDetails(new ProductDetailsUpdateDto(
            "Royal Canin Kitten", "Royal Canin", "", ProductCategory.CatFood, null,
            Enumerable.Range(0, 21).Select(i => $"tag{i}").ToList(),
            TaxClassId, null, true, false, [], TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.TagLimitExceeded, ex.ErrorCode);

        product.UpdateDetails(new ProductDetailsUpdateDto(
            "Royal Canin Kitten", "Royal Canin", "", ProductCategory.CatFood, null, ["Grain Free"],
            TaxClassId, null, true, false, [], TestUserContext.Create(), Guid.NewGuid()));
        Assert.Contains("grain-free", product.Tags);
    }

    [Fact]
    public void UpdateDetails_CategoryChangeWhileActive_Throws()
    {
        var product = CreateStandardProduct();
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "RC-1", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        var facts = new PublicationFacts([product.Skus.Single().Id], true, []);
        product.Publish(facts, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.UpdateDetails(new ProductDetailsUpdateDto(
            "Royal Canin Kitten", "Royal Canin", "", ProductCategory.Litter, null, [],
            TaxClassId, null, true, false, [], TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.CategoryLockedWhileActive, ex.ErrorCode);
    }
}
