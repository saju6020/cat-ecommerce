using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 16 cases that had no implementing test at the TEST gate (2026-10-04):
/// U-A5b (a gif is never a product image), U-B2 (guaranteed-analysis validation) and the domain
/// half of R3 (the four violation codes that replaced the wrong <c>InvalidSlug</c>). The HTTP
/// halves live in <c>ShopSection16GapEndpointTests</c>. Section 18: A1 (U-A5b), A6 (U-B2).
/// </summary>
public sealed class ProductSection16GapTests
{
    private static readonly Guid TaxClassId = Guid.NewGuid();

    private static ProductAggregate CreateProduct(ProductCategory? category = null)
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(
            ProductKind.Standard, "Test Product", "Acme", category ?? ProductCategory.HealthAndGrooming, null,
            TaxClassId, "test-product", null, null, TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    private static ProductDetailsUpdateDto DetailsDto(ProductAggregate product, string? name = null, string? brand = null, string? description = null) =>
        new(name ?? product.Name, brand ?? product.Brand, description ?? product.Description, product.Category, product.Subcategory, product.Tags,
            product.TaxClassId, null, product.ListedOnStorefront, false, [], TestUserContext.Create(), Guid.NewGuid());

    // ── U-A5b ───────────────────────────────────────────────────────────────────────────────────
    // The Product aggregate stores a product image's FileId only (section 1 Amended, Phase A (2));
    // the content type is the StoredFile's rule, which every product image must pass before the
    // handler lets its FileId anywhere near SetImages (SetProductImagesCommandHandler).

    private static StoredFileRegistrationDto FileDto(string contentType, FilePurpose purpose) => new(
        Guid.NewGuid(), purpose, contentType, 1024, new string('a', 64), "cat.file",
        "ProductImage/2026/10/" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), DateTimeOffset.UtcNow,
        "DevelopmentNoOp", null, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void U_A5b_ProductImageFile_WithGifContentType_IsRefused()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            new StoredFileAggregate(Guid.NewGuid()).Register(FileDto("image/gif", FilePurpose.ProductImage)));

        Assert.Equal(BusinessViolationCodes.FileTypeNotAllowed, ex.ErrorCode);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void U_A5b_ProductImageFile_WithTheThreeAllowedTypes_IsAccepted(string contentType)
    {
        var file = new StoredFileAggregate(Guid.NewGuid());

        file.Register(FileDto(contentType, FilePurpose.ProductImage));

        Assert.Equal(contentType, file.ContentType);
    }

    [Fact]
    public void U_A5b_NoProductImageRuleAllowsGif()
    {
        var rule = FileRules.Find(FilePurpose.ProductImage)!;

        Assert.DoesNotContain(rule.AllowedContentTypes, t => t.Equals("image/gif", StringComparison.OrdinalIgnoreCase));
    }

    // ── U-B2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_B2_GuaranteedAnalysis_32_15_4_10_IsAcceptedAndStoredOnTheProduct()
    {
        var product = CreateProduct(ProductCategory.CatFood);

        product.SetFoodDetails(new FoodDetails(null, null, null, null, new GuaranteedAnalysis(32, 15, 4, 10), null, null), TestUserContext.Create(), Guid.NewGuid());

        var analysis = product.FoodDetails!.GuaranteedAnalysis!;
        Assert.Equal((32m, 15m, 4m, 10m), (analysis.ProteinPercent, analysis.FatPercent, analysis.FibrePercent, analysis.MoisturePercent));
    }

    [Fact]
    public void U_B2_GuaranteedAnalysis_60_30_10_5_SumsOverOneHundred_IsInvalid()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => new GuaranteedAnalysis(60, 30, 10, 5));

        Assert.Equal(BusinessViolationCodes.InvalidGuaranteedAnalysis, ex.ErrorCode);
        Assert.Contains("100", ex.Message);
    }

    [Theory]
    [InlineData(101, 0, 0, 0)]
    [InlineData(0, 101, 0, 0)]
    [InlineData(0, 0, 101, 0)]
    [InlineData(0, 0, 0, 101)]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -0.01, 0, 0)]
    public void U_B2_GuaranteedAnalysis_ValueOutsideZeroToOneHundred_IsInvalid(double protein, double fat, double fibre, double moisture)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            new GuaranteedAnalysis((decimal)protein, (decimal)fat, (decimal)fibre, (decimal)moisture));

        Assert.Equal(BusinessViolationCodes.InvalidGuaranteedAnalysis, ex.ErrorCode);
    }

    [Theory]
    [InlineData(100, 0, 0, 0)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(25, 25, 25, 25)]
    public void U_B2_GuaranteedAnalysis_BoundaryValues_AreAccepted(double protein, double fat, double fibre, double moisture)
    {
        var analysis = new GuaranteedAnalysis((decimal)protein, (decimal)fat, (decimal)fibre, (decimal)moisture);

        Assert.Equal((decimal)protein, analysis.ProteinPercent);
    }

    /// <summary>"All or none": the value object cannot be built from three values (the type has no
    /// such constructor), and a product with no analysis reports none -- never a partial one.</summary>
    [Fact]
    public void U_B2_NoAnalysis_IsNull_AndTheTypeHasNoPartialConstructor()
    {
        var product = CreateProduct(ProductCategory.CatFood);
        product.SetFoodDetails(new FoodDetails(null, null, null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());

        Assert.Null(product.FoodDetails!.GuaranteedAnalysis);
        Assert.All(typeof(GuaranteedAnalysis).GetConstructors(), c => Assert.Equal(4, c.GetParameters().Length));
    }

    // ── R3 (domain) ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void R3_Create_BlankName_IsInvalidProductName(string name)
    {
        var product = new ProductAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.Create(new ProductCreateDto(
            ProductKind.Standard, name, "Acme", ProductCategory.HealthAndGrooming, null, TaxClassId, "slug-a", null, null, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidProductName, ex.ErrorCode);
    }

    [Fact]
    public void R3_UpdateDetails_NameOverTwoHundred_IsInvalidProductName()
    {
        var product = CreateProduct();

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.UpdateDetails(DetailsDto(product, name: new string('n', ProductRules.NameMaxLength + 1))));

        Assert.Equal(BusinessViolationCodes.InvalidProductName, ex.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void R3_Create_BlankBrand_IsInvalidBrand(string brand)
    {
        var product = new ProductAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.Create(new ProductCreateDto(
            ProductKind.Standard, "Name", brand, ProductCategory.HealthAndGrooming, null, TaxClassId, "slug-b", null, null, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidBrand, ex.ErrorCode);
    }

    [Fact]
    public void R3_UpdateDetails_BrandOverOneHundred_IsInvalidBrand()
    {
        var product = CreateProduct();

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.UpdateDetails(DetailsDto(product, brand: new string('b', ProductRules.BrandMaxLength + 1))));

        Assert.Equal(BusinessViolationCodes.InvalidBrand, ex.ErrorCode);
    }

    [Fact]
    public void R3_UpdateDetails_DescriptionOverLimit_IsInvalidProductDescription()
    {
        var product = CreateProduct();

        var ex = Assert.Throws<DomainRuleViolationException>(() => product.UpdateDetails(DetailsDto(product, description: new string('d', ProductRules.DescriptionMaxLength + 1))));

        Assert.Equal(BusinessViolationCodes.InvalidProductDescription, ex.ErrorCode);
    }

    [Fact]
    public void R3_UpdateDetails_DescriptionAtTheLimit_IsAccepted()
    {
        var product = CreateProduct();

        product.UpdateDetails(DetailsDto(product, description: new string('d', ProductRules.DescriptionMaxLength)));

        Assert.Equal(ProductRules.DescriptionMaxLength, product.Description.Length);
    }

    [Theory]
    [InlineData("rc-1")]          // lower case
    [InlineData("RC--1")]         // double hyphen
    [InlineData("-RC1")]          // leading hyphen
    [InlineData("RC 1")]          // space
    [InlineData("RC_1")]          // underscore
    [InlineData("")]              // empty
    public void R3_AddSku_MalformedCode_IsInvalidSkuCode(string code)
    {
        var product = CreateProduct();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.AddSku(new SkuAddDto(Guid.NewGuid(), code, null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.InvalidSkuCode, ex.ErrorCode);
    }

    [Fact]
    public void R3_AddSku_CodeOverFortyCharacters_IsInvalidSkuCode()
    {
        var product = CreateProduct();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.AddSku(new SkuAddDto(Guid.NewGuid(), new string('A', ProductRules.SkuCodeMaxLength + 1), null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.InvalidSkuCode, ex.ErrorCode);
    }

    [Fact]
    public void R3_NoneOfTheFourCodes_IsTheOldInvalidSlug()
    {
        Assert.NotEqual(BusinessViolationCodes.InvalidSlug, BusinessViolationCodes.InvalidProductName);
        Assert.NotEqual(BusinessViolationCodes.InvalidSlug, BusinessViolationCodes.InvalidBrand);
        Assert.NotEqual(BusinessViolationCodes.InvalidSlug, BusinessViolationCodes.InvalidProductDescription);
        Assert.NotEqual(BusinessViolationCodes.InvalidSlug, BusinessViolationCodes.InvalidSkuCode);
    }
}
