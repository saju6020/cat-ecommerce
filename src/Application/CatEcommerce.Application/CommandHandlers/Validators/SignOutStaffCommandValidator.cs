using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SignOutStaffCommandValidator : AbstractValidator<SignOutStaffCommand>
{
    public SignOutStaffCommandValidator() => RuleFor(x => x.TokenHash).NotEmpty();
}
