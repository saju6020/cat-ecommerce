using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreateStaffAccountCommandValidator : AbstractValidator<CreateStaffAccountCommand>
{
    public CreateStaffAccountCommandValidator()
    {
        RuleFor(x => x.StaffAccountId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.MobileNumber)
            .Must((command, _) => !string.IsNullOrWhiteSpace(command.MobileNumber) || !string.IsNullOrWhiteSpace(command.EmailAddress))
            .WithMessage("At least one of a mobile number or email address is required.")
            .WithErrorCode(BusinessViolationCodes.IdentifierRequired);

        RuleFor(x => x.MobileNumber).MaximumLength(MobileNumber.MaxLength);
        RuleFor(x => x.EmailAddress).MaximumLength(EmailAddress.MaxLength);

        RuleFor(x => x.RoleIds).NotEmpty().WithErrorCode(BusinessViolationCodes.StaffRolesRequired);
        When(x => x.InitialPassword is not null, () =>
            RuleFor(x => x.InitialPassword)
                .MinimumLength(PasswordRules.MinimumLength)
                .WithErrorCode(BusinessViolationCodes.PasswordTooShort)
                .WithMessage($"Password must be at least {PasswordRules.MinimumLength} characters.")
                .MaximumLength(PasswordRules.MaximumLength));
    }
}
