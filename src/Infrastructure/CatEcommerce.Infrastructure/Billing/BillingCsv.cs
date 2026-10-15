using System.Globalization;
using System.Text;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// The one CSV cell writer of Billing (HO-BIL-001 §12.4 list exports, ADR-190 accounting export): UTF-8 without a BOM,
/// invariant culture, <c>\n</c> line endings, every text cell double-quoted, and the <b>CSV-injection guard</b> of
/// HO-ADM-001 ADR-051 -- a text cell whose first significant character is <c>= + - @</c> (after any leading tab, CR, LF or
/// space) is prefixed with <c>'</c>. Amounts are written as plain invariant numbers (never negative -- direction is
/// carried by the record kind), so they never need the guard.
/// </summary>
public static class BillingCsv
{
    public static string Text(string? value)
    {
        value ??= string.Empty;

        // Skip leading whitespace/control characters a spreadsheet still honours as "leading" (tab, CR, LF, space).
        var firstSignificant = value.AsSpan().TrimStart("\t\r\n ");
        var guarded = firstSignificant.Length > 0 && "=+-@".Contains(firstSignificant[0]) ? "'" + value : value;
        return "\"" + guarded.Replace("\"", "\"\"") + "\"";
    }

    public static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Quantity(decimal value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Date(DateOnly? value) => value is { } date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;

    public static string Instant(DateTimeOffset? value) =>
        value is { } instant ? instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) : string.Empty;

    public static string Bool(bool value) => value ? "true" : "false";

    public static void Row(StringBuilder builder, params string[] cells) => builder.Append(string.Join(",", cells)).Append('\n');
}
