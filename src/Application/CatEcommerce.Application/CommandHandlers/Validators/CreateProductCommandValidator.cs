using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SHP-001 section 11.2 -- shape only. Slug uniqueness, SKU code uniqueness, taxonomy
/// parentage and tax-class existence are aggregate/handler rules.</summary>
public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<CreateProductCommand, ProductKind>();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ProductRules.NameMaxLength).NoControlCharacters();
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(ProductRules.BrandMaxLength).NoControlCharacters();
        RuleFor(x => x.Category).NotEmpty().MustBeValidEnumeration<CreateProductCommand, ProductCategory>();
        RuleFor(x => x.Subcategory).MustBeValidEnumeration<CreateProductCommand, ProductSubcategory>();
        RuleFor(x => x.TaxClassId).NotEmpty();
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(ProductRules.SlugMaxLength).MustBeSlugFormat();

        RuleFor(x => x.BundleSkuId).NotEmpty().When(x => x.Kind == nameof(ProductKind.Bundle));
        RuleFor(x => x.BundleSkuCode).NotEmpty().MaximumLength(ProductRules.SkuCodeMaxLength)
            .When(x => x.Kind == nameof(ProductKind.Bundle));
    }
}
