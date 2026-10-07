using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ACC-001 §11.2 (C2). No minimum length on Password -- that is a strength rule;
/// reporting it on sign-in would say "that can't be your password".</summary>
public sealed class SignInCustomerCommandValidator : AbstractValidator<SignInCustomerCommand>
{
    public SignInCustomerCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}
