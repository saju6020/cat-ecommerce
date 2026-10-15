using System.Globalization;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §8.7 (ADR-179). Pure. A date in July 2026-June 2027 with start month 7 is
/// <c>"2026-27"</c>; with start month 1 it is <c>"2026"</c>.</summary>
public static class FinancialYearRules
{
    public static string For(DateOnly date, int startMonth)
    {
        if (startMonth is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(startMonth));
        if (startMonth == 1) return date.Year.ToString(CultureInfo.InvariantCulture);

        var startYear = date.Month >= startMonth ? date.Year : date.Year - 1;
        return $"{startYear.ToString(CultureInfo.InvariantCulture)}-{((startYear + 1) % 100).ToString("D2", CultureInfo.InvariantCulture)}";
    }

    /// <summary>The first calendar year of a financial-year label, for ordering series.</summary>
    public static int StartYear(string financialYear) =>
        int.Parse(financialYear.Split('-')[0], CultureInfo.InvariantCulture);
}
