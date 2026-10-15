using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ORD-001 §11.2 -- shape only, for returns and refunds (C22-C25). Eligibility (the window, BR-10), the defect confirmation, the refund method table and the
/// reference rules are the policy's, the aggregate's and the handler's.</summary>
public sealed class RequestReturnCommandValidator : AbstractValidator<RequestReturnCommand>
{
    public RequestReturnCommandValidator()
    {
        RuleFor(x => x.ReturnRequestId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= OrderRules.MaxLines).WithMessage($"must have at most {OrderRules.MaxLines} lines.")
            .Must(l => l.Select(x => x.OrderLineId).Distinct().Count() == l.Count).WithMessage("must name each order line once.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).NotEmpty();
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, OrderRules.MaxQuantityPerLineHardCap);
            line.RuleFor(l => l.Reason).Must(r => r is not null).WithMessage("is not a return reason.");
            line.RuleFor(l => l.Condition).Must(c => c is not null).WithMessage("must be Sealed, Opened or Used.");
            line.RuleFor(l => l.CustomerNote).MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
        });
    }
}

public sealed class ApproveReturnCommandValidator : AbstractValidator<ApproveReturnCommand>
{
    public ApproveReturnCommandValidator()
    {
        RuleFor(x => x.ReturnRequestId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).NotEmpty();
            line.RuleFor(l => l.DefectNote).MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
        });
    }
}

public sealed class RejectReturnCommandValidator : AbstractValidator<RejectReturnCommand>
{
    public RejectReturnCommandValidator()
    {
        RuleFor(x => x.ReturnRequestId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ReceiveReturnCommandValidator : AbstractValidator<ReceiveReturnCommand>
{
    public ReceiveReturnCommandValidator()
    {
        RuleFor(x => x.ReturnRequestId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Select(x => x.OrderLineId).Distinct().Count() == l.Count).WithMessage("must name each order line once.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).NotEmpty();
            line.RuleFor(l => l.QuantityReceived).InclusiveBetween(0, OrderRules.MaxQuantityPerLineHardCap);
        });
    }
}

public sealed class IssueOrderRefundCommandValidator : AbstractValidator<IssueRefundCommand>
{
    public IssueOrderRefundCommandValidator()
    {
        RuleFor(x => x.RefundId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ReturnRequestId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        RuleFor(x => x.Method).Must(m => m is not null).WithMessage("must be CashInStore, BankTransfer or StoreCredit.");
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(9_999_999_999.99m).Must(a => decimal.Round(a, 2) == a).WithMessage("must have at most two decimal places.");
        RuleFor(x => x.Reference).MaximumLength(OrderRules.MaxTransferReferenceLength).NoControlCharacters();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
    }
}

/// <summary>HO-ORD-001 §11.2 C26 -- shape only. Whether the tenders cover the total, the stock and the prescription rules are the handler's and the pure services'.</summary>
public sealed class CompleteCounterSaleCommandValidator : AbstractValidator<CompleteCounterSaleCommand>
{
    public CompleteCounterSaleCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(OrderRules.MaxNameLength).NoControlCharacters();
        RuleFor(x => x.CustomerMobile).NotEmpty().MaximumLength(30).NoControlCharacters();
        RuleFor(x => x.DiscountCode).MaximumLength(DiscountCodeRules.MaxCodeLength + 10).NoControlCharacters();
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= OrderRules.MaxLines).WithMessage($"must have at most {OrderRules.MaxLines} lines.")
            .Must(l => l.Select(x => (x.SkuId, x.CatId)).Distinct().Count() == l.Count).WithMessage("must name each item once; raise its quantity instead.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.SkuId).NotEmpty();
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, OrderRules.MaxQuantityPerLineHardCap);
            line.RuleFor(l => l.CatId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        });
        RuleFor(x => x.Tenders).NotEmpty().Must(t => t.Count <= TenderCalculator.MaxTenders).WithMessage($"must have at most {TenderCalculator.MaxTenders} payments.");
        RuleForEach(x => x.Tenders).ChildRules(tender =>
        {
            tender.RuleFor(t => t.Method).Must(m => m is not null).WithMessage("must be Cash or InStoreCard.");
            tender.RuleFor(t => t.Amount).GreaterThan(0m).LessThanOrEqualTo(9_999_999_999.99m).Must(a => decimal.Round(a, 2) == a).WithMessage("must have at most two decimal places.");
            tender.RuleFor(t => t.Reference).MaximumLength(TenderCalculator.MaxReferenceLength).NoControlCharacters();
        });
    }
}
