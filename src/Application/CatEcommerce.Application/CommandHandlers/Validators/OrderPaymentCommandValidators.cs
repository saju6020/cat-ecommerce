using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ORD-001 §11.2 -- shape only, for the payment-path commands (C10-C15, C27). Whether the order awaits a transfer, the exact amount, the second
/// approver, the cancellation window and the card-number rule are the aggregate's and the handler's.</summary>
public sealed class SubmitTransferReferenceCommandValidator : AbstractValidator<SubmitTransferReferenceCommand>
{
    public SubmitTransferReferenceCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(OrderRules.MaxTransferReferenceLength).NoControlCharacters();
    }
}

public sealed class ConfirmBankTransferCommandValidator : AbstractValidator<ConfirmBankTransferCommand>
{
    public ConfirmBankTransferCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(9_999_999_999.99m).Must(a => decimal.Round(a, 2) == a).WithMessage("must have at most two decimal places.");
        RuleFor(x => x.BankReference).NotEmpty().MaximumLength(OrderRules.MaxTransferReferenceLength).NoControlCharacters();
        RuleFor(x => x.ReceivedOn).Must(t => t <= timeProvider.GetUtcNow().AddMinutes(5)).WithMessage("cannot be in the future.");
    }
}

public sealed class ApproveHeldPaymentCommandValidator : AbstractValidator<ApproveHeldPaymentCommand>
{
    public ApproveHeldPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class RejectHeldPaymentCommandValidator : AbstractValidator<RejectHeldPaymentCommand>
{
    public RejectHeldPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ExpireUnpaidOrderCommandValidator : AbstractValidator<ExpireUnpaidOrderCommand>
{
    public ExpireUnpaidOrderCommandValidator() => RuleFor(x => x.OrderId).NotEmpty();
}

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        RuleFor(x => x.Note).MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ReorderCommandValidator : AbstractValidator<ReorderCommand>
{
    public ReorderCommandValidator() => RuleFor(x => x.SourceOrderId).NotEmpty();
}

public sealed class ResolveReconciliationIssueCommandValidator : AbstractValidator<ResolveReconciliationIssueCommand>
{
    public ResolveReconciliationIssueCommandValidator()
    {
        RuleFor(x => x.IssueId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
    }
}
