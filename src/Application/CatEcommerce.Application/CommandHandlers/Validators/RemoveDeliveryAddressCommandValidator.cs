using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RemoveDeliveryAddressCommandValidator : AbstractValidator<RemoveDeliveryAddressCommand>
{
    public RemoveDeliveryAddressCommandValidator() => RuleFor(x => x.AddressId).NotEmpty();
}
