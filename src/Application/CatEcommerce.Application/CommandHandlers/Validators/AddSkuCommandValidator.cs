using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class AddSkuCommandValidator : AbstractValidator<AddSkuCommand>
{
    public AddSkuCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(ProductRules.SkuCodeMaxLength);
        RuleFor(x => x.Barcode).MaximumLength(ProductRules.BarcodeMaxLength);
        RuleFor(x => x.SizeOrWeight).MaximumLength(ProductRules.SizeOrWeightMaxLength);
        RuleFor(x => x.Flavour).MaximumLength(ProductRules.FlavourMaxLength);
        RuleFor(x => x.Colour).MaximumLength(ProductRules.ColourMaxLength);
        RuleFor(x => x.PackQuantity).InclusiveBetween(ProductRules.MinPackQuantity, ProductRules.MaxPackQuantity)
            .When(x => x.PackQuantity is not null);
        RuleFor(x => x.ShippingWeightGrams).InclusiveBetween(ProductRules.MinShippingWeightGrams, ProductRules.MaxShippingWeightGrams)
            .When(x => x.ShippingWeightGrams is not null);
    }
}
