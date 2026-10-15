using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ORD-001 §11.2 -- shape rules shared by the configuration commands: money has at most two decimals, text has no control characters,
/// enumerations and times parse. Everything about <i>meaning</i> (ascending bands, overlapping windows, scope rules) is the aggregate's.</summary>
internal static class OrderValidationRules
{
    public static IRuleBuilderOptions<T, decimal> MustBeMoney<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.InclusiveBetween(0m, 9_999_999_999.99m).Must(v => decimal.Round(v, 2) == v).WithMessage("must have at most two decimal places.");

    public static IRuleBuilderOptions<T, decimal?> MustBeMoney<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.Must(v => v is null || (v >= 0m && v <= 9_999_999_999.99m && decimal.Round(v.Value, 2) == v)).WithMessage("must be zero or more, with at most two decimal places.");
}

/// <summary>ORD-002 SECURITY LOW (backend half) -- a courier tracking link is rendered as an anchor on the customer's order page, so the template must be an absolute
/// <c>http(s)</c> URL: no <c>javascript:</c>/<c>data:</c> scheme, no scheme-relative or relative form. The <c>{tracking}</c> placeholder is substituted before parsing.</summary>
internal static class CourierTrackingUrlRules
{
    public static bool IsAbsoluteHttpTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return true;   // a courier may have no tracking link

        var probe = template.Trim().Replace("{tracking}", "x", StringComparison.Ordinal);
        return Uri.TryCreate(probe, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && !string.IsNullOrEmpty(uri.Host);
    }

    public static IRuleBuilderOptions<T, string?> MustBeAbsoluteHttpUrlTemplate<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(IsAbsoluteHttpTemplate).WithMessage("must be an absolute http or https URL.");
}

public sealed class CreateDeliveryZoneCommandValidator : AbstractValidator<CreateDeliveryZoneCommand>
{
    public CreateDeliveryZoneCommandValidator()
    {
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(DeliveryZoneRules.MaxNameLength).NoControlCharacters();
        RuleFor(x => x.Priority).InclusiveBetween(0, 10_000);
        RuleFor(x => x.CodMaxOrderValue).MustBeMoney();
        RuleFor(x => x.Coverage).NotNull();
        RuleFor(x => x.Services).NotNull();
        RuleForEach(x => x.Coverage).SetValidator(new CoverageInputValidator());
        RuleForEach(x => x.Services).SetValidator(new ZoneServiceInputValidator());
    }
}

public sealed class UpdateDeliveryZoneCommandValidator : AbstractValidator<UpdateDeliveryZoneCommand>
{
    public UpdateDeliveryZoneCommandValidator()
    {
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(DeliveryZoneRules.MaxNameLength).NoControlCharacters();
        RuleFor(x => x.Priority).InclusiveBetween(0, 10_000);
        RuleFor(x => x.CodMaxOrderValue).MustBeMoney();
        RuleFor(x => x.Coverage).NotNull();
        RuleFor(x => x.Services).NotNull();
        RuleForEach(x => x.Coverage).SetValidator(new CoverageInputValidator());
        RuleForEach(x => x.Services).SetValidator(new ZoneServiceInputValidator());
    }
}

public sealed class SetDeliveryZoneActivationCommandValidator : AbstractValidator<SetDeliveryZoneActivationCommand>
{
    public SetDeliveryZoneActivationCommandValidator() => RuleFor(x => x.ZoneId).NotEmpty();
}

public sealed class CoverageInputValidator : AbstractValidator<CoverageInput>
{
    public CoverageInputValidator()
    {
        RuleFor(x => x.District).NotEmpty().MaximumLength(DeliveryZoneRules.MaxDistrictLength).NoControlCharacters();
        RuleForEach(x => x.PostalCodes).NotEmpty().MaximumLength(DeliveryZoneRules.MaxPostalCodeLength).NoControlCharacters();
    }
}

public sealed class ZoneServiceInputValidator : AbstractValidator<ZoneServiceInput>
{
    public ZoneServiceInputValidator()
    {
        RuleFor(x => x.ServiceLevel).NotEmpty().MustBeValidEnumeration<ZoneServiceInput, DeliveryServiceLevel>();
        RuleFor(x => x.BaseCharge).MustBeMoney();
        RuleFor(x => x.FreeOverOrderValue).MustBeMoney();
        RuleFor(x => x.LeadTimeDays).InclusiveBetween(0, DeliveryZoneRules.MaxLeadTimeDays);
        RuleForEach(x => x.WeightBands).ChildRules(band =>
        {
            band.RuleFor(b => b.Surcharge).MustBeMoney();
            band.RuleFor(b => b.UpToGrams).Must(g => g is null || g > 0).WithMessage("must be above zero.");
        });
        RuleForEach(x => x.SlotWindows).ChildRules(window =>
        {
            window.RuleFor(w => w.DayOfWeek).Must(DeliveryZoneMapper.IsDayName).WithMessage("must be a day name.");
            window.RuleFor(w => w.Start).Must(v => DeliveryZoneMapper.TryParseTime(v, out _)).WithMessage("must be a time such as 10:00.");
            window.RuleFor(w => w.End).Must(v => DeliveryZoneMapper.TryParseTime(v, out _)).WithMessage("must be a time such as 13:00.");
        });
    }
}

public sealed class CreateCourierCommandValidator : AbstractValidator<CreateCourierCommand>
{
    public CreateCourierCommandValidator()
    {
        RuleFor(x => x.CourierId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(OrderRules.MaxNameLength).NoControlCharacters();
        RuleFor(x => x.TrackingUrlTemplate).MaximumLength(500).NoControlCharacters().MustBeAbsoluteHttpUrlTemplate();
    }
}

public sealed class UpdateCourierCommandValidator : AbstractValidator<UpdateCourierCommand>
{
    public UpdateCourierCommandValidator()
    {
        RuleFor(x => x.CourierId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(OrderRules.MaxNameLength).NoControlCharacters();
        RuleFor(x => x.TrackingUrlTemplate).MaximumLength(500).NoControlCharacters().MustBeAbsoluteHttpUrlTemplate();
    }
}

public sealed class SetBankTransferAccountCommandValidator : AbstractValidator<SetBankTransferAccountCommand>
{
    public SetBankTransferAccountCommandValidator()
    {
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(50).NoControlCharacters();
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.BranchName).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.RoutingNumber).MaximumLength(50).NoControlCharacters();
    }
}

public sealed class CreateDiscountCodeCommandValidator : AbstractValidator<CreateDiscountCodeCommand>
{
    public CreateDiscountCodeCommandValidator()
    {
        RuleFor(x => x.DiscountCodeId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(DiscountCodeRules.MaxCodeLength + 10).NoControlCharacters();
        RuleFor(x => x.Description).MaximumLength(DiscountCodeRules.MaxDescriptionLength).NoControlCharacters();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<CreateDiscountCodeCommand, DiscountKind>();
        RuleFor(x => x.Scope).NotEmpty().MustBeValidEnumeration<CreateDiscountCodeCommand, DiscountScope>();
        RuleFor(x => x.Amount).MustBeMoney();
        RuleFor(x => x.MinimumSpend).MustBeMoney();
        RuleFor(x => x.ProductIds).NotNull().Must(ids => ids.Count <= DiscountCodeRules.MaxScopeIds);
        RuleFor(x => x.CategoryIds).NotNull().Must(ids => ids.Count <= DiscountCodeRules.MaxScopeIds);
    }
}

public sealed class UpdateDiscountCodeCommandValidator : AbstractValidator<UpdateDiscountCodeCommand>
{
    public UpdateDiscountCodeCommandValidator()
    {
        RuleFor(x => x.DiscountCodeId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(DiscountCodeRules.MaxDescriptionLength).NoControlCharacters();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<UpdateDiscountCodeCommand, DiscountKind>();
        RuleFor(x => x.Scope).NotEmpty().MustBeValidEnumeration<UpdateDiscountCodeCommand, DiscountScope>();
        RuleFor(x => x.Amount).MustBeMoney();
        RuleFor(x => x.MinimumSpend).MustBeMoney();
        RuleFor(x => x.ProductIds).NotNull().Must(ids => ids.Count <= DiscountCodeRules.MaxScopeIds);
        RuleFor(x => x.CategoryIds).NotNull().Must(ids => ids.Count <= DiscountCodeRules.MaxScopeIds);
    }
}

public sealed class SetDiscountCodeActivationCommandValidator : AbstractValidator<SetDiscountCodeActivationCommand>
{
    public SetDiscountCodeActivationCommandValidator() => RuleFor(x => x.DiscountCodeId).NotEmpty();
}
