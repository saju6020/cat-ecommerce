using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ORD-001 §11.2 -- shape only, for the fulfilment commands (C16-C21). Whether the move is allowed, the batches are usable and the money adds up are
/// the aggregate's and the handler's.</summary>
public sealed class StartPackingCommandValidator : AbstractValidator<StartPackingCommand>
{
    public StartPackingCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class DispatchOrderCommandValidator : AbstractValidator<DispatchOrderCommand>
{
    public DispatchOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CourierId).NotEmpty();
        RuleFor(x => x.TrackingNumber).MaximumLength(OrderRules.MaxTrackingNumberLength).NoControlCharacters();
    }
}

public sealed class MarkReadyForCollectionCommandValidator : AbstractValidator<MarkReadyForCollectionCommand>
{
    public MarkReadyForCollectionCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class RecordTrackingNumberCommandValidator : AbstractValidator<RecordTrackingNumberCommand>
{
    public RecordTrackingNumberCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(OrderRules.MaxTrackingNumberLength).NoControlCharacters();
    }
}

public sealed class RecordDeliveryCommandValidator : AbstractValidator<RecordDeliveryCommand>
{
    public RecordDeliveryCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DeliveredOn).Must(t => t <= timeProvider.GetUtcNow().AddMinutes(5)).WithMessage("cannot be in the future.");
        RuleFor(x => x).Must(x => x.CodCollection is null || x.CollectionPayment is null).WithMessage("Send either the cash collected on delivery or a payment taken at collection, not both.");

        When(x => x.CodCollection is not null, () =>
        {
            RuleFor(x => x.CodCollection!.Amount).GreaterThan(0m).LessThanOrEqualTo(9_999_999_999.99m).Must(a => decimal.Round(a, 2) == a).WithMessage("must have at most two decimal places.");
            RuleFor(x => x.CodCollection!.CollectorName).MaximumLength(OrderRules.MaxRecipientNameLength).NoControlCharacters();
            RuleFor(x => x.CodCollection!.CollectedOn).Must(t => t <= timeProvider.GetUtcNow().AddMinutes(5)).WithMessage("cannot be in the future.");
            RuleFor(x => x.CodCollection!.CollectorKind).Must(k => k is not null).WithMessage("must be Courier or Staff.");
            RuleFor(x => x.CodCollection!).Must(c => c.CollectorKind == CollectorKind.Staff ? c.CollectorStaffId is not null : !string.IsNullOrWhiteSpace(c.CollectorName))
                .WithMessage("name the courier who collected the cash, or the staff member.");
        });

        When(x => x.CollectionPayment is not null, () =>
        {
            RuleFor(x => x.CollectionPayment!.Amount).GreaterThan(0m).LessThanOrEqualTo(9_999_999_999.99m).Must(a => decimal.Round(a, 2) == a).WithMessage("must have at most two decimal places.");
            RuleFor(x => x.CollectionPayment!.Method).Must(m => m is not null).WithMessage("must be Cash or InStoreCard.");
            RuleFor(x => x.CollectionPayment!.Reference).MaximumLength(OrderRules.MaxTerminalReferenceLength).NoControlCharacters();
        });
    }
}

public sealed class RecordDeliveryFailureCommandValidator : AbstractValidator<RecordDeliveryFailureCommand>
{
    public RecordDeliveryFailureCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).Must(r => r is not null).WithMessage("must be Refused, Undeliverable or Uncollected.");
        RuleFor(x => x.Note).MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ApplyCourierTrackingEventCommandValidator : AbstractValidator<ApplyCourierTrackingEventCommand>
{
    public ApplyCourierTrackingEventCommandValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.EventId).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(OrderRules.MaxTrackingNumberLength).NoControlCharacters();
        RuleFor(x => x.Status).NotEmpty().MaximumLength(100).NoControlCharacters();
    }
}

public sealed class ReallocateOrderLineCommandValidator : AbstractValidator<ReallocateOrderLineCommand>
{
    public ReallocateOrderLineCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.TargetBatchId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(OrderRules.MaxReasonLength).NoControlCharacters();
    }
}
