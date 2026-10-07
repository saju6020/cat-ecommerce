using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class AddDeliveryAddressCommandValidator : AbstractValidator<AddDeliveryAddressCommand>
{
    public AddDeliveryAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(DeliveryAddressRules.LabelMaxLength);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(DeliveryAddressRules.Line1MaxLength);
        RuleFor(x => x.Line2).MaximumLength(DeliveryAddressRules.Line2MaxLength);
        RuleFor(x => x.City).NotEmpty().MaximumLength(DeliveryAddressRules.CityMaxLength);
        RuleFor(x => x.District).NotEmpty().MaximumLength(DeliveryAddressRules.DistrictMaxLength);
        RuleFor(x => x.PostalCode).MaximumLength(DeliveryAddressRules.PostalCodeMaxLength);
        RuleFor(x => x.DeliveryInstructions).MaximumLength(DeliveryAddressRules.DeliveryInstructionsMaxLength);
    }
}
