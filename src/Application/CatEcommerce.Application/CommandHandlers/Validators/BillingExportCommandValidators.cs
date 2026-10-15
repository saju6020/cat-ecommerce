using CatEcommerce.Application.Commands.Billing;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordAccountingExportRunCommandValidator : AbstractValidator<RecordAccountingExportRunCommand>
{
    public RecordAccountingExportRunCommandValidator()
    {
        RuleFor(x => x.RunId).NotEmpty();
        RuleFor(x => x.From).Must(d => d.Year >= 2000).WithMessage("is not a valid date.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x.Sha256).NotEmpty().Length(64).Matches("^[0-9a-fA-F]{64}$");
        RuleFor(x => x.InvoiceCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InvoiceLineCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaymentCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AllocationCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditNoteCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RefundCount).GreaterThanOrEqualTo(0);
    }
}
