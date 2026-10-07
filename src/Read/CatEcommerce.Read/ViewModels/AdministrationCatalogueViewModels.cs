namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-ADM-001 §12.4 -- money as <c>{ amount, currencyCode }</c>, amount a string, never a JSON number.</summary>
public sealed record MoneyViewModel(string Amount, string CurrencyCode);

public sealed record PublicServiceViewModel(
    Guid Id, string Code, string Name, string Description, string Category, int DurationMinutes,
    bool IsBookableOnline, MoneyViewModel? CurrentPrice);

public sealed record AdminServiceViewModel(
    Guid Id, string Code, string Name, string Description, string Category, int DurationMinutes,
    IReadOnlyList<string> QualifiedRoleIds, string RequiredRoomKind, Guid TaxClassId, bool IsBookableOnline,
    bool IsActive, bool IsPriced);

public sealed record ServicePriceViewModel(Guid Id, Guid ServiceId, MoneyViewModel Amount, DateTimeOffset EffectiveFrom, Guid SetBy);

public sealed record TaxClassViewModel(Guid Id, string Code, string Name, bool IsActive);

public sealed record TaxRateViewModel(Guid Id, Guid TaxClassId, decimal RatePercent, DateTimeOffset EffectiveFrom);

public sealed record SettingViewModel(
    string Key, string Group, string Type, string? Min, string? Max, string? Unit, string Default,
    bool BranchOverridable, string DisplayName, string Description, string GlobalValue, string? BranchValue, string EffectiveValue);
