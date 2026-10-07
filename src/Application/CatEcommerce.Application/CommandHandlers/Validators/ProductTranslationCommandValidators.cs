using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>Shape only (section 11.2): the locale pattern <c>^[a-z]{2}(-[A-Z]{2})?$</c> and lengths.
/// Whether the locale is enabled / the default, and markup validity, are the aggregate's.</summary>
public sealed class SetProductTranslationCommandValidator : AbstractValidator<SetProductTranslationCommand>
{
    public SetProductTranslationCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Locale).NotEmpty().Matches("^[a-z]{2}(-[A-Z]{2})?$").WithMessage("Locale must look like 'bn' or 'pt-BR'.");
        RuleFor(x => x.Name).MaximumLength(ProductRules.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(ProductRules.DescriptionMaxLength);
        RuleFor(x => x.IngredientList).MaximumLength(ProductRules.IngredientListMaxLength);
        RuleFor(x => x.FeedingGuide).MaximumLength(ProductRules.FeedingGuideMaxLength);
    }
}

public sealed class RemoveProductTranslationCommandValidator : AbstractValidator<RemoveProductTranslationCommand>
{
    public RemoveProductTranslationCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Locale).NotEmpty().Matches("^[a-z]{2}(-[A-Z]{2})?$").WithMessage("Locale must look like 'bn' or 'pt-BR'.");
    }
}
