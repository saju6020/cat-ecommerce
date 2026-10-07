using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RefreshCustomerSessionCsrfTokenCommandValidator : AbstractValidator<RefreshCustomerSessionCsrfTokenCommand>
{
    public RefreshCustomerSessionCsrfTokenCommandValidator() => RuleFor(x => x.TokenHash).NotEmpty();
}
