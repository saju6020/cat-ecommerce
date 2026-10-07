using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 §15 -- U-H7 (translations) and the translation half of U-A10 (a translated
/// name joins SearchText).</summary>
public sealed class ProductTranslationTests
{
    private static readonly string[] Enabled = ["en", "bn"];

    private static ProductAggregate NewProduct()
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Cat Tree", "PurrCare", ProductCategory.ToysAndFurniture, null,
            Guid.NewGuid(), "cat-tree", null, null, TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    private static void Set(ProductAggregate product, string locale, string? name = null, string? description = null, string? ingredients = null, string? feeding = null) =>
        product.SetTranslation(new ProductTranslationInputDto(locale, name, description, ingredients, feeding), "en", Enabled, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void U_H7_TheDefaultLocale_IsNotTranslatable()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => Set(NewProduct(), "en", "Cat Tree"));
        Assert.Equal(BusinessViolationCodes.DefaultLocaleNotTranslatable, ex.ErrorCode);
    }

    [Fact]
    public void U_H7_ALocaleThatIsNotEnabled_IsRefused()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => Set(NewProduct(), "fr", "Arbre à chat"));
        Assert.Equal(BusinessViolationCodes.LocaleNotEnabled, ex.ErrorCode);
    }

    [Fact]
    public void U_H7_AnEnabledLocale_IsStored_ReplacedOnTheNextSet_AndRaisesTheEvent()
    {
        var product = NewProduct();

        Set(product, "bn", "বিড়ালের গাছ", "বিবরণ");
        Set(product, "bn", "নতুন নাম");

        var translation = Assert.Single(product.Translations);
        Assert.Equal("নতুন নাম", translation.Name);
        Assert.Null(translation.Description);
        Assert.Contains(product.Events, e => e is CatEcommerce.Domain.Events.Product.ProductTranslationChangedEvent { Locale: "bn" });
    }

    [Fact]
    public void U_H7_AnEmptyTranslation_RemovesTheLocale()
    {
        var product = NewProduct();
        Set(product, "bn", "বিড়ালের গাছ");

        Set(product, "bn", "  ", "", null, null);

        Assert.Empty(product.Translations);
    }

    [Fact]
    public void U_H7_RemoveTranslation_RemovesIt_AndAnAbsentOneIsANoOp()
    {
        var product = NewProduct();
        Set(product, "bn", "বিড়ালের গাছ");

        product.RemoveTranslation("bn", "en", Enabled, TestUserContext.Create(), Guid.NewGuid());
        product.RemoveTranslation("bn", "en", Enabled, TestUserContext.Create(), Guid.NewGuid());

        Assert.Empty(product.Translations);
    }

    [Fact]
    public void U_H7_RemoveTranslation_OfTheDefaultOrADisabledLocale_IsRefused()
    {
        var product = NewProduct();

        Assert.Equal(BusinessViolationCodes.DefaultLocaleNotTranslatable,
            Assert.Throws<DomainRuleViolationException>(() => product.RemoveTranslation("en", "en", Enabled, TestUserContext.Create(), Guid.NewGuid())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.LocaleNotEnabled,
            Assert.Throws<DomainRuleViolationException>(() => product.RemoveTranslation("fr", "en", Enabled, TestUserContext.Create(), Guid.NewGuid())).ErrorCode);
    }

    [Fact]
    public void U_H7_TranslatedTextIsValidated_MarkupAndLength()
    {
        var product = NewProduct();

        var badMarkup = Assert.Throws<DomainRuleViolationException>(() => Set(product, "bn", description: "<script>alert(1)</script>"));
        Assert.NotEqual(BusinessViolationCodes.DefaultLocaleNotTranslatable, badMarkup.ErrorCode);

        var tooLong = Assert.Throws<DomainRuleViolationException>(() => Set(product, "bn", name: new string('x', ProductRules.NameMaxLength + 1)));
        Assert.Equal(BusinessViolationCodes.InvalidTranslationContent, tooLong.ErrorCode);

        var control = Assert.Throws<DomainRuleViolationException>(() => Set(product, "bn", ingredients: "chicken\0"));
        Assert.Equal(BusinessViolationCodes.InvalidTranslationContent, control.ErrorCode);
    }

    [Fact]
    public void U_A10_ATranslatedNameJoinsSearchText_AndLeavesItWhenRemoved()
    {
        var product = NewProduct();

        Set(product, "bn", "BengaliNameToken");
        Assert.Contains("bengalinametoken", product.SearchText);

        product.RemoveTranslation("bn", "en", Enabled, TestUserContext.Create(), Guid.NewGuid());
        Assert.DoesNotContain("bengalinametoken", product.SearchText);
    }
}
