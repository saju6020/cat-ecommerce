namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1 / §8.7. <c>CurrencyCode</c> added by §10.2 for <c>commerce.currencyCode</c>.</summary>
public sealed class SettingType(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SettingType Integer = new(1, nameof(Integer));
    public static readonly SettingType Decimal = new(2, nameof(Decimal));
    public static readonly SettingType Boolean = new(3, nameof(Boolean));
    public static readonly SettingType TimeRange = new(4, nameof(TimeRange));
    public static readonly SettingType CurrencyCode = new(5, nameof(CurrencyCode));
}
