using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-BIL-001 §11.2 -- shape only. Method-dependent requirements (reference, collector), the threshold, the caps and the
/// card-number check are aggregate or handler rules.</summary>
public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Method).NotEmpty().MustBeValidEnumeration<RecordPaymentCommand, PaymentMethod>();
        RuleFor(x => x.Amount).MoneyShape(allowZero: false);
        RuleFor(x => x.ReceivedOn).Must(d => BillingShapeRules.NotMoreThanADayAhead(d, timeProvider)).WithMessage("cannot be in the future.");
        RuleFor(x => x.ExternalReference).MaximumLength(BillingRules.MaxReferenceLength).NoControlCharacters();
        RuleFor(x => x.Notes).MaximumLength(BillingRules.MaxNotesLength).NoControlCharacters();
        RuleFor(x => x.IdempotencyKey).IdempotencyKeyShape();

        When(x => x.Collector is not null, () =>
        {
            RuleFor(x => x.Collector!.Kind).NotEmpty().MustBeValidEnumeration<RecordPaymentCommand, CollectorKind>();
            RuleFor(x => x.Collector!.CourierCode).MaximumLength(BillingRules.MaxCourierCodeLength).NoControlCharacters();
            RuleFor(x => x.Collector!.CourierName).MaximumLength(BillingRules.MaxCourierNameLength).NoControlCharacters();
        });

        When(x => x.Earmark is not null, () =>
        {
            RuleFor(x => x.Earmark!.Kind).NotEmpty().MustBeValidEnumeration<RecordPaymentCommand, EarmarkKind>();
            RuleFor(x => x.Earmark!.SourceId).NotEmpty();
            RuleFor(x => x.Earmark!.Label).NotEmpty().MaximumLength(BillingRules.MaxEarmarkLabelLength).NoControlCharacters();
        });

        RuleFor(x => x.Allocations).NotNull().Must(a => a is { Count: <= BillingRules.MaxRequestedAllocations })
            .WithMessage($"cannot name more than {BillingRules.MaxRequestedAllocations} invoices.");
        RuleForEach(x => x.Allocations).ChildRules(allocation =>
        {
            allocation.RuleFor(a => a.InvoiceId).NotEmpty();
            allocation.RuleFor(a => a.Amount).MoneyShape(allowZero: false);
        });
    }
}

public sealed class ApprovePaymentCommandValidator : AbstractValidator<ApprovePaymentCommand>
{
    public ApprovePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class RejectPaymentCommandValidator : AbstractValidator<RejectPaymentCommand>
{
    public RejectPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(BillingRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class AllocatePaymentCommandValidator : AbstractValidator<AllocatePaymentCommand>
{
    public AllocatePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Allocations).NotNull().Must(a => a is { Count: >= 1 and <= BillingRules.MaxRequestedAllocations })
            .WithMessage($"must name between 1 and {BillingRules.MaxRequestedAllocations} invoices.");
        RuleForEach(x => x.Allocations).ChildRules(allocation =>
        {
            allocation.RuleFor(a => a.InvoiceId).NotEmpty();
            allocation.RuleFor(a => a.Amount).MoneyShape(allowZero: false);
        });
    }
}

public sealed class ReleasePaymentEarmarkCommandValidator : AbstractValidator<ReleasePaymentEarmarkCommand>
{
    public ReleasePaymentEarmarkCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(BillingRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ReversePaymentCommandValidator : AbstractValidator<ReversePaymentCommand>
{
    public ReversePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(BillingRules.MaxReasonLength).NoControlCharacters();
    }
}
