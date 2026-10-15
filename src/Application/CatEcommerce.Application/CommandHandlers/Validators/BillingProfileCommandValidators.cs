using CatEcommerce.Application.Commands.Billing;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-BIL-001 §11.2 -- shape only. Bank account numbers are the business's own, so no card-number
/// check applies to them (§11.4 C1).</summary>
public sealed class SetBillingProfileCommandValidator : AbstractValidator<SetBillingProfileCommand>
{
    public SetBillingProfileCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.TaxRegistrationNumber).NotEmpty().MaximumLength(50).NoControlCharacters();
        RuleFor(x => x.AddressLines).NotNull().Must(l => l is { Count: >= 1 and <= 5 }).WithMessage("must have one to five lines.");
        RuleForEach(x => x.AddressLines).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.Phone).MaximumLength(30).NoControlCharacters();
        RuleFor(x => x.Email).MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.InvoiceFooter).MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.BankAccounts).NotNull().Must(a => a is { Count: <= 5 }).WithMessage("must have at most five accounts.");
        RuleForEach(x => x.BankAccounts).ChildRules(account =>
        {
            account.RuleFor(a => a.BankName).NotEmpty().MaximumLength(100).NoControlCharacters();
            account.RuleFor(a => a.AccountName).NotEmpty().MaximumLength(100).NoControlCharacters();
            account.RuleFor(a => a.AccountNumber).NotEmpty().MaximumLength(50).NoControlCharacters();
            account.RuleFor(a => a.BranchName).MaximumLength(100).NoControlCharacters();
            account.RuleFor(a => a.RoutingNumber).MaximumLength(30).NoControlCharacters();
        });
    }
}
