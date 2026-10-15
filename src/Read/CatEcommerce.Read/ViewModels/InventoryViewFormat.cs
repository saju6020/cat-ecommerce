using System.Globalization;

namespace CatEcommerce.Read.ViewModels;

/// <summary>
/// HO-IMP-001 §12.4 -- the wire shape of Inventory's numbers. QUANTITIES are strings ("12.000" is sent as "12", "12.500" as "12.5") with
/// <c>stockUnit</c> beside them; money is HO-ADM-001's <c>{ amount: "1500.00", currencyCode }</c>; UNIT COSTS are the same with four
/// decimals (<c>"12.3456"</c>) because a per-unit cost is a rate, not an amount charged (ADR-106).
/// </summary>
public static class InventoryViewFormat
{
    public static string Quantity(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string? Quantity(decimal? value) => value is null ? null : Quantity(value.Value);

    public static MoneyViewModel Money(decimal amount, string currencyCode) =>
        new(amount.ToString("F2", CultureInfo.InvariantCulture), currencyCode);

    public static MoneyViewModel UnitCost(decimal amount, string currencyCode) =>
        new(amount.ToString("F4", CultureInfo.InvariantCulture), currencyCode);
}
