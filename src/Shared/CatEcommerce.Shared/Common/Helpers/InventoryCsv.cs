using System.Globalization;
using System.Text;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-IMP-001 §12.4 -- the CSV writer of the inventory exports, with HO-ADM-001 ADR-051's CSV-injection guard: a TEXT cell beginning <c>= + - @</c>
/// (optionally after a tab, CR, LF or space a spreadsheet still treats as leading) is prefixed with <c>'</c>. Typed numbers and dates are written
/// as invariant text without the guard -- they cannot carry a formula, and a negative quantity must stay a number.
/// </summary>
public static class InventoryCsv
{
    public static string Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", headers.Select(h => Cell(h))));
        sb.Append("\r\n");

        foreach (var row in rows)
        {
            sb.Append(string.Join(",", row.Select(Cell)));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    public static string Cell(object? value) => value switch
    {
        null => "\"\"",
        string text => Quote(Guard(text)),
        decimal number => Quote(number.ToString(CultureInfo.InvariantCulture)),
        int number => Quote(number.ToString(CultureInfo.InvariantCulture)),
        long number => Quote(number.ToString(CultureInfo.InvariantCulture)),
        bool flag => Quote(flag ? "true" : "false"),
        DateOnly date => Quote(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        DateTimeOffset moment => Quote(moment.ToString("O", CultureInfo.InvariantCulture)),
        Guid id => Quote(id.ToString()),
        _ => Quote(Guard(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty))
    };

    /// <summary>The guard on its own (U-X4).</summary>
    public static string Guard(string text)
    {
        var firstSignificant = text.AsSpan().TrimStart("\t\r\n ");
        return firstSignificant.Length > 0 && "=+-@".Contains(firstSignificant[0]) ? "'" + text : text;
    }

    private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
}
