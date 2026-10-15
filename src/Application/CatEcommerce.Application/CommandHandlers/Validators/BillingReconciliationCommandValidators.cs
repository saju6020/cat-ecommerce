using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-BIL-001 section 11.2 -- shape only. Whether each line is a COD collection of this courier and not over-remitted is the handler.</summary>
public sealed class RecordCodRemittanceCommandValidator : AbstractValidator<RecordCodRemittanceCommand>
{
    public RecordCodRemittanceCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.RemittanceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CourierCode).NotEmpty().MaximumLength(BillingRules.MaxCourierCodeLength).NoControlCharacters();
        RuleFor(x => x.ReceivedOn).Must(d => BillingShapeRules.NotMoreThanADayAhead(d, timeProvider)).WithMessage("cannot be in the future.");
        RuleFor(x => x.Amount).MoneyShape(allowZero: false);
        RuleFor(x => x.Method).NotEmpty().MustBeValidEnumeration<RecordCodRemittanceCommand, RemittanceMethod>();
        RuleFor(x => x.Reference).MaximumLength(BillingRules.MaxReferenceLength).NoControlCharacters();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= BillingRules.MaxRemittanceLines })
            .WithMessage($"must name between 1 and {BillingRules.MaxRemittanceLines} collections.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PaymentId).NotEmpty();
            line.RuleFor(l => l.AmountRemitted).MoneyShape(allowZero: false);
        });
    }
}

public sealed class ImportBankStatementCommandValidator : AbstractValidator<ImportBankStatementCommand>
{
    public ImportBankStatementCommandValidator()
    {
        RuleFor(x => x.StatementId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.BankAccountLabel).NotEmpty().MaximumLength(100).NoControlCharacters();
        RuleFor(x => x.SourceFileName).MaximumLength(255).NoControlCharacters();
        RuleFor(x => x.SourceSha256).NotEmpty().Length(64);
        RuleFor(x => x.PeriodTo).GreaterThanOrEqualTo(x => x.PeriodFrom);
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= BillingRules.MaxStatementLines })
            .WithMessage($"must have between 1 and {BillingRules.MaxStatementLines} lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Amount).MoneyShape(allowZero: false);
            line.RuleFor(l => l.Direction).NotEmpty().MustBeValidEnumeration<StatementLineInput, BankEntryDirection>();
        });
    }
}

public sealed class AutoMatchBankStatementCommandValidator : AbstractValidator<AutoMatchBankStatementCommand>
{
    public AutoMatchBankStatementCommandValidator()
    {
        RuleFor(x => x.StatementId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class MatchBankLineCommandValidator : AbstractValidator<MatchBankLineCommand>
{
    public MatchBankLineCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.StatementId).NotEmpty();
        RuleFor(x => x.StatementLineId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class UnmatchBankLineCommandValidator : AbstractValidator<UnmatchBankLineCommand>
{
    public UnmatchBankLineCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(BillingRules.MaxReasonLength).NoControlCharacters();
    }
}
