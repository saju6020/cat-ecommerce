using System.Globalization;

namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-ORD-001 §12.4 -- money as <c>{ amount, currencyCode }</c>, the amount a two-decimal string (HO-ADM-001 §12.4).</summary>
public static class OrderMoney
{
    public static MoneyViewModel Of(decimal amount, string currencyCode) =>
        new(amount.ToString("F2", CultureInfo.InvariantCulture), currencyCode);

    public static MoneyViewModel? Of(decimal? amount, string currencyCode) => amount is { } value ? Of(value, currencyCode) : null;
}

public sealed record WeightBandViewModel(int? UpToGrams, MoneyViewModel Surcharge);

public sealed record SlotWindowViewModel(string DayOfWeek, string Start, string End, string Label);

public sealed record ZoneServiceViewModel(
    string ServiceLevel, bool IsActive, MoneyViewModel BaseCharge, MoneyViewModel? FreeOverOrderValue, int LeadTimeDays,
    IReadOnlyList<WeightBandViewModel> WeightBands, IReadOnlyList<SlotWindowViewModel> SlotWindows);

public sealed record ZoneCoverageViewModel(string District, IReadOnlyList<string> PostalCodes);

public sealed record DeliveryZoneSummaryViewModel(
    Guid Id, Guid BranchId, string Name, int Priority, bool IsActive, bool CodEnabled, int CoverageCount, IReadOnlyList<string> ServiceLevels);

public sealed record DeliveryZoneViewModel(
    Guid Id, Guid BranchId, string Name, int Priority, bool IsActive, bool CodEnabled, MoneyViewModel? CodMaxOrderValue, string CurrencyCode, int Version,
    IReadOnlyList<ZoneCoverageViewModel> Coverage, IReadOnlyList<ZoneServiceViewModel> Services);

/// <summary>The admin test tool's answer: which zone an address falls in (<c>Matched</c>), or <c>AddressOutsideServedZones</c>.</summary>
public sealed record ResolvedDeliveryZoneViewModel(bool Matched, Guid? ZoneId, string? ZoneName, Guid? BranchId, string? ReasonCode);

public sealed record CourierViewModel(Guid Id, string Name, string? TrackingUrlTemplate, bool SupportsCod, bool IsActive);

public sealed record BankTransferAccountViewModel(
    bool Configured, string? AccountName, string? AccountNumber, string? BankName, string? BranchName, string? RoutingNumber, bool IsActive);

public sealed record DiscountCodeViewModel(
    Guid Id, string Code, string? Description, string Kind, decimal? Percent, MoneyViewModel? Amount, MoneyViewModel? MinimumSpend, string Scope,
    IReadOnlyList<Guid> ProductIds, IReadOnlyList<int> CategoryIds, bool FirstOrderOnly, int? UsageLimit, int? PerCustomerLimit,
    DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, bool IsActive, int TimesRedeemed, int Version);
