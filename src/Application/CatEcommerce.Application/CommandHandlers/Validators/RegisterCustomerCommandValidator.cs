using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ACC-001 §11.2 (C1). Shape only -- nothing needing a database, nothing revealing
/// whether an account exists.</summary>
public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.MobileNumber)
            .Must((command, _) => !string.IsNullOrWhiteSpace(command.MobileNumber) || !string.IsNullOrWhiteSpace(command.EmailAddress))
            .WithMessage("At least one of a mobile number or email address is required.")
            .WithErrorCode(BusinessViolationCodes.IdentifierRequired);

        RuleFor(x => x.MobileNumber).MaximumLength(MobileNumber.MaxLength);
        RuleFor(x => x.EmailAddress).MaximumLength(EmailAddress.MaxLength);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(PasswordRules.MinimumLength)
            .WithErrorCode(BusinessViolationCodes.PasswordTooShort)
            .WithMessage($"Password must be at least {PasswordRules.MinimumLength} characters.")
            .MaximumLength(PasswordRules.MaximumLength);
    }
}
