using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SetFoodDetailsCommandValidator : AbstractValidator<SetFoodDetailsCommand>
{
    public SetFoodDetailsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Manufacturer).MaximumLength(ProductRules.ManufacturerMaxLength);
        RuleFor(x => x.LifeStage).MustBeValidEnumeration<SetFoodDetailsCommand, LifeStage>();
        RuleFor(x => x.ProteinSource).MustBeValidEnumeration<SetFoodDetailsCommand, ProteinSource>();
        RuleFor(x => x.IngredientList).MaximumLength(ProductRules.IngredientListMaxLength);
        RuleFor(x => x.FeedingGuide).MaximumLength(ProductRules.FeedingGuideMaxLength);
        RuleFor(x => x.StorageInstructions).MaximumLength(ProductRules.StorageInstructionsMaxLength);
    }
}
