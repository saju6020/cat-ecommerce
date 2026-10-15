using System.Globalization;
using System.Text;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Infrastructure.Reporting;

/// <summary>
/// HO-RPT-001 section 8.12 (ADR-285) -- UTF-8 <b>with</b> a byte-order mark (so spreadsheet tools read Bengali names correctly), CRLF line endings, a header row of the visible column labels,
/// money as a plain <c>0.00</c> number with a separate currency column, instants in the report zone as <c>yyyy-MM-dd HH:mm</c>. A text cell beginning <c>= + - @</c>, a tab or a carriage return is
/// prefixed with <c>'</c> -- HO-ADM-001 ADR-051's rule, called from <see cref="InventoryCsv.Guard"/> (the shared guard) and not copied. Numbers are never guarded, so a credit note's negative amount
/// stays a number. The row cap is the pipeline's job (the handler refuses before this runs).
/// </summary>
public sealed class ReportCsvWriter : IReportExportRenderer
{
    public const string CurrencyHeader = "Currency";

    public ReportFormat Format => ReportFormat.Csv;
    public string ContentType => "text/csv; charset=utf-8";
    public string Extension => "csv";

    public byte[] Render(ReportResult result, ReportRenderContext context) => Build(result);

    public static byte[] Build(ReportResult result)
    {
        var zone = ResolveZone(result.Filters.TimeZoneId);
        var hasMoney = result.Columns.Any(c => c.Type == ReportColumnType.Money);

        var builder = new StringBuilder();
        builder.Append(string.Join(",", result.Columns.Select(c => Text(c.Label)).Concat(hasMoney ? [Text(CurrencyHeader)] : [])));
        builder.Append("\r\n");

        foreach (var row in result.Rows)
        {
            var cells = result.Columns.Select(c => Cell(c, row.GetValueOrDefault(c.Key), zone)).ToList();
            if (hasMoney) cells.Add(Text(row.Values.OfType<ReportMoney>().FirstOrDefault()?.CurrencyCode ?? ""));
            builder.Append(string.Join(",", cells));
            builder.Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    /// <summary>A text cell: the shared ADR-051 guard, plus a leading tab or carriage return (a spreadsheet reads either as the start of a formula), then quoted.</summary>
    public static string Text(string? value)
    {
        value ??= string.Empty;
        var guarded = value.Length > 0 && (value[0] == '\t' || value[0] == '\r') ? "'" + value : InventoryCsv.Guard(value);
        return "\"" + guarded.Replace("\"", "\"\"") + "\"";
    }

    public static string Cell(ReportColumn column, object? value, TimeZoneInfo zone)
    {
        if (value is null) return string.Empty;

        return value switch
        {
            ReportMoney money => money.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset instant => TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            bool flag => flag ? "Yes" : "No",
            decimal number when column.Type == ReportColumnType.Percent => number.ToString("0.0", CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            int or long or short => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            _ => Text(value.ToString()),
        };
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
