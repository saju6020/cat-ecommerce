using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class UpdateCustomerContactDetailsCommandValidator : AbstractValidator<UpdateCustomerContactDetailsCommand>
{
    public UpdateCustomerContactDetailsCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmailAddress).MaximumLength(EmailAddress.MaxLength);
        RuleFor(x => x.AdditionalPhoneNumbers).Must(n => n.Count <= CustomerProfileRules.MaxAdditionalPhoneNumbers)
            .WithMessage($"No more than {CustomerProfileRules.MaxAdditionalPhoneNumbers} additional phone numbers are allowed.");
        RuleForEach(x => x.AdditionalPhoneNumbers).MaximumLength(MobileNumber.MaxLength);
    }
}
