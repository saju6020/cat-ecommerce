using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordStaffSessionActivityCommandValidator : AbstractValidator<RecordStaffSessionActivityCommand>
{
    public RecordStaffSessionActivityCommandValidator() => RuleFor(x => x.TokenHash).NotEmpty();
}
