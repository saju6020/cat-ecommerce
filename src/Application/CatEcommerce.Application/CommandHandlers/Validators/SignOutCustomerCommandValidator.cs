using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SignOutCustomerCommandValidator : AbstractValidator<SignOutCustomerCommand>
{
    public SignOutCustomerCommandValidator() => RuleFor(x => x.TokenHash).NotEmpty();
}
