using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SetStaffPasswordCommandValidator : AbstractValidator<SetStaffPasswordCommand>
{
    public SetStaffPasswordCommandValidator()
    {
        RuleFor(x => x.StaffAccountId).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(PasswordRules.MinimumLength)
            .WithErrorCode(BusinessViolationCodes.PasswordTooShort)
            .WithMessage($"Password must be at least {PasswordRules.MinimumLength} characters.")
            .MaximumLength(PasswordRules.MaximumLength);
    }
}
