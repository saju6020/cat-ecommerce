namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-RPT-001-FU1 -- the typed, server-computed direction of a reconciliation variance (R13 payment reconciliation, R14 daily cash-up). Serialised as its name (<c>"Over"</c>, <c>"Short"</c>,
/// <c>"Matched"</c>). The direction is always read off the two figures the report compares -- what was received or counted against what was expected -- never off the sign of the variance cell,
/// because R13's cash-on-delivery and bank-transfer rows show <c>expected - received</c> while R13's cash row and R14 show <c>counted - expected</c>.
/// </summary>
public enum VarianceDirection
{
    /// <summary>Equal to the cent: nothing to explain (also a variance of 0, 0.00 or -0.00).</summary>
    Matched = 0,

    /// <summary>More was received or counted than expected.</summary>
    Over = 1,

    /// <summary>Less was received or counted than expected.</summary>
    Short = 2,
}

public static class VarianceDirections
{
    /// <summary>The column key both reports carry beside <c>variance</c>.</summary>
    public const string ColumnKey = "varianceDirection";

    /// <summary><c>Over</c> when <paramref name="actual"/> (received or counted) exceeds <paramref name="expected"/>, <c>Short</c> when it is lower, otherwise <c>Matched</c>. Decimal comparison: -0.00 equals 0.</summary>
    public static VarianceDirection Of(decimal actual, decimal expected) => actual > expected ? VarianceDirection.Over : actual < expected ? VarianceDirection.Short : VarianceDirection.Matched;

    /// <summary>The direction when both figures exist, otherwise <c>null</c> -- a day not counted or a method with nothing to compare against has no direction, never <c>Matched</c>.</summary>
    public static VarianceDirection? Of(decimal? actual, decimal? expected) => actual is { } a && expected is { } e ? Of(a, e) : null;

    /// <summary>The direction as report rows carry it (a string sorts and serialises without ceremony in the generic pipeline): the enum name, or <c>null</c> when there is nothing to compare.</summary>
    public static string? Name(decimal? actual, decimal? expected) => Of(actual, expected)?.ToString();

    /// <summary>The same, when both figures are known to exist.</summary>
    public static string Name(decimal actual, decimal expected) => Of(actual, expected).ToString();
}
