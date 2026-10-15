using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-BRD-001 section 11.2 -- shape only: ids, lengths, enum membership, sign and scale of money. Availability, capacity-by-type, overlaps and
// back-dating are aggregate or handler rules. Registered in BoardingModule as IValidator<T> (the platform resolves the interface).

public sealed class CreateBoardingUnitCommandValidator : AbstractValidator<CreateBoardingUnitCommand>
{
    public CreateBoardingUnitCommandValidator()
    {
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(StayRules.MaxUnitCodeLength).NoControlCharacters();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(StayRules.MaxUnitNameLength).NoControlCharacters();
        RuleFor(x => x.Type).NotEmpty().MustBeValidEnumeration<CreateBoardingUnitCommand, BoardingUnitType>();
        RuleFor(x => x.CapacityCats).InclusiveBetween(1, StayRules.MaxUnitCapacity);
        RuleFor(x => x.Block).MaximumLength(StayRules.MaxUnitBlockLength).NoControlCharacters();
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class UpdateBoardingUnitCommandValidator : AbstractValidator<UpdateBoardingUnitCommand>
{
    public UpdateBoardingUnitCommandValidator()
    {
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(StayRules.MaxUnitNameLength).NoControlCharacters();
        RuleFor(x => x.Type).NotEmpty().MustBeValidEnumeration<UpdateBoardingUnitCommand, BoardingUnitType>();
        RuleFor(x => x.CapacityCats).InclusiveBetween(1, StayRules.MaxUnitCapacity);
        RuleFor(x => x.Block).MaximumLength(StayRules.MaxUnitBlockLength).NoControlCharacters();
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class SetBoardingUnitActivationCommandValidator : AbstractValidator<SetBoardingUnitActivationCommand>
{
    public SetBoardingUnitActivationCommandValidator()
    {
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CreateMaintenanceBlockCommandValidator : AbstractValidator<CreateMaintenanceBlockCommand>
{
    public CreateMaintenanceBlockCommandValidator()
    {
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(StayRules.MaxMaintenanceReasonLength).NoControlCharacters();
    }
}

public sealed class CancelMaintenanceBlockCommandValidator : AbstractValidator<CancelMaintenanceBlockCommand>
{
    public CancelMaintenanceBlockCommandValidator()
    {
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class SetBoardingRateCardCommandValidator : AbstractValidator<SetBoardingRateCardCommand>
{
    public SetBoardingRateCardCommandValidator()
    {
        RuleFor(x => x.RateCardId).NotEmpty();
        RuleFor(x => x.UnitType).NotEmpty().MustBeValidEnumeration<SetBoardingRateCardCommand, BoardingUnitType>();
        RuleFor(x => x.TaxClassId).NotEmpty();
        RuleFor(x => x.NightlyRate).InclusiveBetween(0m, BillingRules.MaxAmount).MustHaveAtMostTwoDecimals();
        RuleFor(x => x.WeeklyRate).InclusiveBetween(0m, BillingRules.MaxAmount).MustHaveAtMostTwoDecimals();
        RuleFor(x => x.MonthlyRate).InclusiveBetween(0m, BillingRules.MaxAmount).MustHaveAtMostTwoDecimals();
        RuleFor(x => x.AdditionalCatNightlyRate).InclusiveBetween(0m, BillingRules.MaxAmount).MustHaveAtMostTwoDecimals();
    }
}

public sealed class SetBoardingCareRoutineCommandValidator : AbstractValidator<SetBoardingCareRoutineCommand>
{
    public SetBoardingCareRoutineCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Times).NotNull().Must(t => t.Count <= BoardingRoutineDefaults.MaxTimes).WithMessage($"A routine has at most {BoardingRoutineDefaults.MaxTimes} times.");
        RuleForEach(x => x.Times).ChildRules(time => time.RuleFor(t => t.Kind).NotEmpty().MustBeValidEnumeration<RoutineTimeItem, RoutineTaskKind>());
    }
}

internal static class BoardingValidationExtensions
{
    /// <summary>Money is <c>decimal(12,2)</c> (HO-ADM-001 ADR-054): more than two decimals is a 400 here, not a silent rounding later.</summary>
    public static IRuleBuilderOptions<T, decimal> MustHaveAtMostTwoDecimals<T>(this IRuleBuilderOptions<T, decimal> rule) =>
        rule.Must(value => decimal.Round(value, 2) == value).WithMessage("must have at most two decimal places.");
}
