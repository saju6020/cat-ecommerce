using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting;

/// <summary>
/// HO-RPT-001 section 12.4 -- the JSON shape of a <see cref="ReportResult"/>: money as <c>{ amount: "1500.00", currencyCode }</c>, percentages as strings with one decimal place, decimals as
/// strings, integers as numbers, dates <c>yyyy-MM-dd</c>, instants ISO-8601 with the offset of the report's zone, and a value the system does not record as <c>null</c> (never 0).
/// No floating point anywhere (C-6).
/// </summary>
public static class ReportResultFormatter
{
    public static object ToJson(ReportResult result)
    {
        var zone = SafeZone(result.Filters.TimeZoneId);

        return new
        {
            reportKey = result.ReportKey,
            title = result.Title,
            generatedOn = result.GeneratedOn,
            generatedBy = result.GeneratedBy,
            filters = new
            {
                from = result.Filters.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                to = result.Filters.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                branchId = result.Filters.BranchId,
                branchName = result.Filters.BranchName,
                timeZoneId = result.Filters.TimeZoneId,
                groupBy = result.Filters.GroupBy,
                parameters = result.Filters.Parameters.ToDictionary(p => p.Key, p => (object?)ParameterValue(p.Value)),
            },
            columns = result.Columns.Select(c => new { key = c.Key, label = c.Label, type = c.Type.DisplayName, unit = c.Unit, sortable = c.Sortable, description = c.Description }).ToList(),
            rows = result.Rows.Select(r => ToJsonRow(r, result.Columns, zone)).ToList(),
            totals = result.Totals is null ? null : ToJsonRow(result.Totals, result.Columns, zone),
            notes = result.Notes.Select(n => new { code = n.Code, text = n.Text }).ToList(),
            coverage = result.Coverage is null ? null : new
            {
                costedRevenue = Money(result.Coverage.CostedRevenue),
                totalRevenue = Money(result.Coverage.TotalRevenue),
                percent = result.Coverage.Percent.ToString("0.0", CultureInfo.InvariantCulture),
            },
            pageNumber = result.PageNumber,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
        };
    }

    public static Dictionary<string, object?> ToJsonRow(ReportRow row, IReadOnlyList<ReportColumn> columns, TimeZoneInfo zone)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var column in columns)
        {
            if (!row.TryGetValue(column.Key, out var value)) continue;
            output[column.Key] = Cell(column, value, zone);
        }

        return output;
    }

    public static object? Cell(ReportColumn column, object? value, TimeZoneInfo zone)
    {
        if (value is null) return null;

        if (value is ReportMoney money) return Money(money);

        if (column.Type == ReportColumnType.Percent) return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("0.0", CultureInfo.InvariantCulture);
        if (column.Type == ReportColumnType.Decimal) return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        if (column.Type == ReportColumnType.Integer) return Convert.ToInt64(value, CultureInfo.InvariantCulture);

        return value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset instant => TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            bool flag => flag,
            _ => value.ToString(),
        };
    }

    public static object Money(ReportMoney money) => new { amount = money.Amount.ToString("0.00", CultureInfo.InvariantCulture), currencyCode = money.CurrencyCode };

    private static object? ParameterValue(object value) => value is Guid or int or bool ? value : value.ToString();

    private static TimeZoneInfo SafeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
