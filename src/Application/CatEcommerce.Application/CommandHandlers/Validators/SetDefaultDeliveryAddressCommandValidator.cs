using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SetDefaultDeliveryAddressCommandValidator : AbstractValidator<SetDefaultDeliveryAddressCommand>
{
    public SetDefaultDeliveryAddressCommandValidator() => RuleFor(x => x.AddressId).NotEmpty();
}
