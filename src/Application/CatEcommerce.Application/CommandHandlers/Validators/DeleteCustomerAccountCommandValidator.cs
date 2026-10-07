using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class DeleteCustomerAccountCommandValidator : AbstractValidator<DeleteCustomerAccountCommand>
{
    public DeleteCustomerAccountCommandValidator() =>
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(256);
}
