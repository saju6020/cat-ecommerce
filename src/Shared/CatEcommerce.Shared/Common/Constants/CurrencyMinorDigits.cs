namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ORD-001 §8.5 / OQ-5 (ADR-198) -- the number of minor-unit digits of an ISO-4217 currency. Ordering computes in <c>long</c> minor
/// units and converts to ADR-054's <c>decimal(12,2)</c> exactly, which is only lossless for a two-digit currency: a configured currency
/// with any other digit count is refused at start-up (<c>commerce.currencyCode</c>), never silently mis-scaled.
/// </summary>
public static class CurrencyMinorDigits
{
    /// <summary>The scale ADR-054 stores: two decimals.</summary>
    public const int SupportedDigits = 2;

    private static readonly Dictionary<string, int> Digits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BDT"] = 2, ["USD"] = 2, ["EUR"] = 2, ["GBP"] = 2, ["INR"] = 2, ["PKR"] = 2, ["LKR"] = 2, ["NPR"] = 2, ["AED"] = 2, ["SAR"] = 2,
        ["SGD"] = 2, ["MYR"] = 2, ["AUD"] = 2, ["CAD"] = 2, ["CNY"] = 2, ["THB"] = 2,
        ["JPY"] = 0, ["KRW"] = 0, ["VND"] = 0,
        ["KWD"] = 3, ["BHD"] = 3, ["OMR"] = 3, ["JOD"] = 3, ["TND"] = 3,
    };

    /// <summary>The minor digits of <paramref name="currencyCode"/>, or <c>null</c> when the code is not in the table.</summary>
    public static int? For(string currencyCode) => Digits.TryGetValue(currencyCode, out var digits) ? digits : null;

    /// <summary>True when Ordering can price in this currency: known, and exactly <see cref="SupportedDigits"/> minor digits.</summary>
    public static bool IsSupported(string currencyCode) => For(currencyCode) == SupportedDigits;
}
