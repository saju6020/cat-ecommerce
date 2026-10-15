using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordCashCountCommandValidator : AbstractValidator<RecordCashCountCommand>
{
    public RecordCashCountCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.BusinessDate).Must(d => d.Year >= 2000).WithMessage("is not a valid date.");
        RuleFor(x => x.CountedCash).MoneyShape(allowZero: true);
        RuleFor(x => x.Explanation).MaximumLength(BillingRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class CloseCashUpCommandValidator : AbstractValidator<CloseCashUpCommand>
{
    public CloseCashUpCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.BusinessDate).Must(d => d.Year >= 2000).WithMessage("is not a valid date.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}
