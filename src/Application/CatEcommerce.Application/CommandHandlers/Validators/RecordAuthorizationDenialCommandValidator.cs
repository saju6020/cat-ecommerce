using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordAuthorizationDenialCommandValidator : AbstractValidator<RecordAuthorizationDenialCommand>
{
    public RecordAuthorizationDenialCommandValidator()
    {
        RuleFor(x => x.Action).NotEmpty();
        RuleFor(x => x.Method).NotEmpty();
        RuleFor(x => x.Path).NotEmpty();
    }
}
