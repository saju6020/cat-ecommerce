using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RefreshStaffSessionCsrfTokenCommandValidator : AbstractValidator<RefreshStaffSessionCsrfTokenCommand>
{
    public RefreshStaffSessionCsrfTokenCommandValidator() => RuleFor(x => x.TokenHash).NotEmpty();
}
