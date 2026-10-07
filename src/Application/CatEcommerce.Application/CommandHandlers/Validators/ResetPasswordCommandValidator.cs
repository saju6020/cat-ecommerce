using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(PasswordRules.MinimumLength)
            .WithErrorCode(BusinessViolationCodes.PasswordTooShort)
            .WithMessage($"Password must be at least {PasswordRules.MinimumLength} characters.")
            .MaximumLength(PasswordRules.MaximumLength);
    }
}
