using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>Shared money-shape rules (HO-BIL-001 §11.2): amounts carry at most two decimals, quantities at most three.</summary>
internal static class BillingShapeRules
{
    public static bool HasAtMostDecimals(decimal value, int decimals) => decimal.Round(value, decimals) == value;

    public static IRuleBuilderOptions<T, decimal> MoneyShape<T>(this IRuleBuilder<T, decimal> rule, bool allowZero) =>
        rule.Must(v => (allowZero ? v >= 0m : v > 0m) && v <= BillingRules.MaxAmount && HasAtMostDecimals(v, 2))
            .WithMessage(allowZero ? "must not be negative and have at most two decimals." : "must be greater than zero with at most two decimals.");

    public static IRuleBuilderOptions<T, decimal> QuantityShape<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.Must(v => v > 0m && v <= 9_999_999m && HasAtMostDecimals(v, 3)).WithMessage("must be greater than zero with at most three decimals.");

    /// <summary>The Billing date rule at the shape level: not more than one day ahead <b>by UTC</b> -- the precise
    /// branch-local rule is the handler's (§11.2).</summary>
    public static bool NotMoreThanADayAhead(DateOnly date, TimeProvider timeProvider) => date <= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(1);
}

public sealed class RecordManualChargeCommandValidator : AbstractValidator<RecordManualChargeCommand>
{
    public RecordManualChargeCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.ChargeId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Quantity).QuantityShape();
        RuleFor(x => x.LineDiscount).MoneyShape(allowZero: true);

        RuleFor(x => x).Must(c => c.ServiceId is not null != (c.Description is not null))
            .WithMessage("Give either a service or a description, price and tax class -- not both and not neither.");
        When(x => x.ServiceId is null, () =>
        {
            RuleFor(x => x.Description).NotEmpty().MaximumLength(BillingRules.MaxDescriptionLength).NoControlCharacters();
            RuleFor(x => x.UnitPrice).NotNull();
            RuleFor(x => x.UnitPrice).Must(p => p is null || (p >= 0m && p <= BillingRules.MaxAmount && BillingShapeRules.HasAtMostDecimals(p.Value, 2))).WithMessage("must not be negative and have at most two decimals.");
            RuleFor(x => x.TaxClassId).NotNull().NotEqual(Guid.Empty);
        });
        When(x => x.ServiceId is not null, () =>
        {
            RuleFor(x => x.ServiceId).NotEqual(Guid.Empty);
            RuleFor(x => x.UnitPrice).Null().WithMessage("must not be set when a service is chosen.");
            RuleFor(x => x.TaxClassId).Null().WithMessage("must not be set when a service is chosen.");
        });

        RuleFor(x => x.BusinessLine).MustBeValidEnumeration<RecordManualChargeCommand, BusinessLine>().When(x => x.BusinessLine is not null);
        RuleFor(x => x.ServiceDate).Must(d => BillingShapeRules.NotMoreThanADayAhead(d, timeProvider)).WithMessage("cannot be in the future.");
    }
}

public sealed class VoidChargeCommandValidator : AbstractValidator<VoidChargeCommand>
{
    public VoidChargeCommandValidator()
    {
        RuleFor(x => x.ChargeId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(ChargeRules.MaxVoidReasonLength).NoControlCharacters();
    }
}

public sealed class CaptureSourceChargesCommandValidator : AbstractValidator<CaptureSourceChargesCommand>
{
    public CaptureSourceChargesCommandValidator()
    {
        RuleFor(x => x.SourceKind).NotEmpty().MustBeValidEnumeration<CaptureSourceChargesCommand, ChargeSourceKind>();
        RuleFor(x => x.SourceId).NotEmpty();
    }
}
