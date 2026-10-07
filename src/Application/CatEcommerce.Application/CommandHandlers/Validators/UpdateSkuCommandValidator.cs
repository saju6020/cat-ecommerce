using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class UpdateSkuCommandValidator : AbstractValidator<UpdateSkuCommand>
{
    public UpdateSkuCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.SizeOrWeight).MaximumLength(ProductRules.SizeOrWeightMaxLength);
        RuleFor(x => x.Flavour).MaximumLength(ProductRules.FlavourMaxLength);
        RuleFor(x => x.Colour).MaximumLength(ProductRules.ColourMaxLength);
        RuleFor(x => x.PackQuantity).InclusiveBetween(ProductRules.MinPackQuantity, ProductRules.MaxPackQuantity)
            .When(x => x.PackQuantity is not null);
    }
}
