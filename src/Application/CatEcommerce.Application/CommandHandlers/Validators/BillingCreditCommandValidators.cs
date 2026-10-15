using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-BIL-001 §11.2 -- shape only. What may still be credited is read from the invoice and earlier credit notes in the handler.</summary>
public sealed class IssueCreditNoteCommandValidator : AbstractValidator<IssueCreditNoteCommand>
{
    public IssueCreditNoteCommandValidator()
    {
        RuleFor(x => x.CreditNoteId).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<IssueCreditNoteCommand, CreditNoteKind>();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(BillingRules.MaxReasonLength).NoControlCharacters();
        RuleFor(x => x.IdempotencyKey).IdempotencyKeyShape();

        RuleFor(x => x.Lines).Must(lines => lines is null || lines.Count <= BillingRules.MaxCreditNoteLines)
            .WithMessage($"cannot name more than {BillingRules.MaxCreditNoteLines} lines.");
        RuleFor(x => x).Must(x => x.FullCredit || x.Lines is { Count: > 0 })
            .WithName("Lines").WithMessage("Choose a full credit or name the lines to credit.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.InvoiceLineId).NotEmpty();
            line.RuleFor(l => l).Must(l => (l.Quantity is null) != (l.Amount is null))
                .WithName("Quantity").WithMessage("Give either a quantity or an amount for each line.");
            line.RuleFor(l => l.Quantity).Must(q => q is null || (q > 0m && q <= 9_999_999m && BillingShapeRules.HasAtMostDecimals(q.Value, 3))).WithMessage("must be greater than zero with at most three decimals.");
            line.RuleFor(l => l.Amount).Must(a => a is null || (a > 0m && a <= BillingRules.MaxAmount && BillingShapeRules.HasAtMostDecimals(a.Value, 2))).WithMessage("must be greater than zero with at most two decimals.");
        });
    }
}

public sealed class CreateCorrectedDraftCommandValidator : AbstractValidator<CreateCorrectedDraftCommand>
{
    public CreateCorrectedDraftCommandValidator()
    {
        RuleFor(x => x.NewInvoiceId).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.CreditNoteId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class SetCorrectedDraftLinesCommandValidator : AbstractValidator<SetCorrectedDraftLinesCommand>
{
    public SetCorrectedDraftLinesCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= BillingRules.MaxInvoiceLines })
            .WithMessage($"must have between 1 and {BillingRules.MaxInvoiceLines} lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(BillingRules.MaxDescriptionLength).NoControlCharacters();
            line.RuleFor(l => l.Quantity).QuantityShape();
            line.RuleFor(l => l.UnitPrice).MoneyShape(allowZero: true);
            line.RuleFor(l => l.LineDiscount).MoneyShape(allowZero: true);
            line.RuleFor(l => l.TaxClassId).NotEmpty();
        });
    }
}

public sealed class IssueRefundCommandValidator : AbstractValidator<IssueRefundCommand>
{
    public IssueRefundCommandValidator()
    {
        RuleFor(x => x.RefundId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Method).NotEmpty().MustBeValidEnumeration<IssueRefundCommand, RefundMethod>();
        RuleFor(x => x.Amount).MoneyShape(allowZero: false);
        RuleFor(x => x.Reference).MaximumLength(BillingRules.MaxReferenceLength).NoControlCharacters();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(BillingRules.MaxReasonLength).NoControlCharacters();
        RuleFor(x => x.IdempotencyKey).IdempotencyKeyShape();
    }
}
