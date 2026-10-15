using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class JoinBoardingWaitlistCommandValidator : AbstractValidator<JoinBoardingWaitlistCommand>
{
    public JoinBoardingWaitlistCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DepartureOn).GreaterThan(x => x.ArrivalOn);
        RuleFor(x => x.CatCount).InclusiveBetween(1, StayRules.MaxCatsPerStay);
        RuleFor(x => x.UnitType).MustBeValidEnumeration<JoinBoardingWaitlistCommand, BoardingUnitType>().When(x => x.UnitType is not null);
    }
}

public sealed class WithdrawBoardingWaitlistCommandValidator : AbstractValidator<WithdrawBoardingWaitlistCommand>
{
    public WithdrawBoardingWaitlistCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public sealed class NotifyWaitlistEntryCommandValidator : AbstractValidator<NotifyWaitlistEntryCommand>
{
    public NotifyWaitlistEntryCommandValidator() => RuleFor(x => x.EntryId).NotEmpty();
}

public sealed class CloseStaleBoardingWaitlistCommandValidator : AbstractValidator<CloseStaleBoardingWaitlistCommand>
{
    public CloseStaleBoardingWaitlistCommandValidator() => RuleFor(x => x.EntryId).NotEmpty();
}

public sealed class SetDaycareRateCardCommandValidator : AbstractValidator<SetDaycareRateCardCommand>
{
    public SetDaycareRateCardCommandValidator()
    {
        RuleFor(x => x.RateCardId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.TaxClassId).NotEmpty();
        RuleFor(x => x.HourlyRate).InclusiveBetween(0m, BillingRules.MaxAmount);
        RuleFor(x => x.DayRate).InclusiveBetween(0m, BillingRules.MaxAmount);
    }
}

public sealed class BookDaycareVisitCommandValidator : AbstractValidator<BookDaycareVisitCommand>
{
    public BookDaycareVisitCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.Basis).NotEmpty().MustBeValidEnumeration<BookDaycareVisitCommand, DaycareBasis>();
        RuleFor(x => x.ExpectedHours).InclusiveBetween(1, 12).When(x => x.ExpectedHours is not null);
    }
}

public sealed class CheckInDaycareVisitCommandValidator : AbstractValidator<CheckInDaycareVisitCommand>
{
    public CheckInDaycareVisitCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CheckOutDaycareVisitCommandValidator : AbstractValidator<CheckOutDaycareVisitCommand>
{
    public CheckOutDaycareVisitCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CancelDaycareVisitCommandValidator : AbstractValidator<CancelDaycareVisitCommand>
{
    public CancelDaycareVisitCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x).Must(x => (x.CustomerId is null) != (x.BranchId is null)).WithMessage("Give either the customer or the branch.");
    }
}

public sealed class RecordDaycareNoShowCommandValidator : AbstractValidator<RecordDaycareNoShowCommand>
{
    public RecordDaycareNoShowCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class SendBoardingDailyUpdateCommandValidator : AbstractValidator<SendBoardingDailyUpdateCommand>
{
    public SendBoardingDailyUpdateCommandValidator() => RuleFor(x => x.StayId).NotEmpty();
}
