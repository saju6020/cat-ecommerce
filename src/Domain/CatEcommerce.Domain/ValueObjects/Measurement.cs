using System.Globalization;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-EMR-001 §12.4 / UI-8 -- every measurement travels as <c>{ value, unit }</c>, the value a
/// <i>string at the stated precision</i> (<c>"4.20"</c>), so no bare number ever appears in a
/// clinical payload and no rounding is left to a client.
/// </summary>
public sealed record Measurement(string Value, string Unit)
{
    public static Measurement Of(decimal value, int decimalPlaces, string unit) =>
        new(value.ToString("F" + decimalPlaces, CultureInfo.InvariantCulture), unit);

    public static Measurement? OfNullable(decimal? value, int decimalPlaces, string unit) =>
        value is null ? null : Of(value.Value, decimalPlaces, unit);
}
