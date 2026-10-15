using CatEcommerce.Application.Commands.Billing;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ApplyLoyaltyRedemptionCommandValidator : AbstractValidator<ApplyLoyaltyRedemptionCommand>
{
    public ApplyLoyaltyRedemptionCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Points).GreaterThan(0).LessThanOrEqualTo(1_000_000);
    }
}

public sealed class RemoveLoyaltyRedemptionCommandValidator : AbstractValidator<RemoveLoyaltyRedemptionCommand>
{
    public RemoveLoyaltyRedemptionCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class ApplyCustomerCreditCommandValidator : AbstractValidator<ApplyCustomerCreditCommand>
{
    public ApplyCustomerCreditCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Amount!.Value).MoneyShape(allowZero: false).When(x => x.Amount is not null);
    }
}
