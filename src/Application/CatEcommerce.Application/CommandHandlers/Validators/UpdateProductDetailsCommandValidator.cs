using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class UpdateProductDetailsCommandValidator : AbstractValidator<UpdateProductDetailsCommand>
{
    public UpdateProductDetailsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ProductRules.NameMaxLength).NoControlCharacters();
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(ProductRules.BrandMaxLength).NoControlCharacters();
        RuleFor(x => x.Description).MaximumLength(ProductRules.DescriptionMaxLength);
        RuleFor(x => x.Category).NotEmpty().MustBeValidEnumeration<UpdateProductDetailsCommand, ProductCategory>();
        RuleFor(x => x.Subcategory).MustBeValidEnumeration<UpdateProductDetailsCommand, ProductSubcategory>();
        RuleFor(x => x.TaxClassId).NotEmpty();
        RuleForEach(x => x.HealthFocus).MustBeValidEnumeration<UpdateProductDetailsCommand, HealthFocus>();
        RuleFor(x => x.Tags).Must(t => t.Count <= ProductRules.MaxTags)
            .WithMessage($"A product may have at most {ProductRules.MaxTags} tags.");
        RuleFor(x => x.HealthFocus).Must(t => t.Count <= ProductRules.MaxHealthFocus)
            .WithMessage($"A product may have at most {ProductRules.MaxHealthFocus} health focus values.");
    }
}
