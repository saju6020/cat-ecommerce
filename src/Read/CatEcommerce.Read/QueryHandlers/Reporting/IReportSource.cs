using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting;

/// <summary>
/// Everything a report source needs, already validated and resolved by the pipeline: the inclusive local date range and its instant range in the report zone, the branch (or all branches),
/// the typed parameters, the columns the caller may see, and the row limit (the format cap plus one in export mode).
/// </summary>
public sealed record ReportRequest(
    ReportDefinition Definition,
    DateOnly From,
    DateOnly To,
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    Guid? BranchId,
    bool AllBranches,
    TimeZoneInfo Zone,
    TimeBucket? GroupBy,
    IReadOnlyDictionary<string, object> Parameters,
    IReadOnlyCollection<string> VisibleColumnKeys,
    int RowLimit,
    DateTimeOffset Now,
    string CurrencyCode,
    IBusinessSettings Settings)
{
    public string TimeZoneId => Zone.Id;

    /// <summary>True when the caller may see the column; a source skips the work for one they may not.</summary>
    public bool IsVisible(string columnKey) => VisibleColumnKeys.Contains(columnKey);

    public bool Flag(string name) => Parameters.TryGetValue(name, out var value) && value is true;

    public int Number(string name) => Parameters.TryGetValue(name, out var value) && value is int number ? number : 0;

    public string Text(string name) => Parameters.TryGetValue(name, out var value) ? value.ToString() ?? "" : "";

    /// <summary>The local date of an instant in the report zone.</summary>
    public DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>The label of the time bucket a local date falls into (day, Monday-start week, or month).</summary>
    public string BucketLabel(DateOnly date) => ReportBuckets.Label(date, GroupBy ?? TimeBucket.Day);
}

/// <summary>What a source returns: rows keyed by column key, optional totals, notes and margin coverage. Sorting, paging, restricted columns and the caps are the pipeline's job, not the source's.</summary>
public sealed record ReportSourceResult(
    IReadOnlyList<ReportRow> Rows, ReportRow? Totals = null, IReadOnlyList<ReportNote>? Notes = null, ReportCoverage? Coverage = null);

/// <summary>
/// HO-RPT-001 section 12.6 -- one class per catalogue key (U-A2 enforces the 1:1), registered explicitly. A source <c>Select</c>s from the keyless <c>report.rv_*</c> view types with explicit
/// projections, aggregates in SQL where it can, and calls the pure domain services the report names. <b>Never</b> a repository, a save, a bulk update or delete, or raw SQL (U-A3).
/// </summary>
public interface IReportSource
{
    string Key { get; }

    Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct);
}

/// <summary>Bucketing of a local date for the time-bucketed reports. Weeks start Monday; the label is the first day of the bucket.</summary>
public static class ReportBuckets
{
    public static DateOnly Start(DateOnly date, TimeBucket bucket)
    {
        if (bucket == TimeBucket.Week) return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        if (bucket == TimeBucket.Month) return new DateOnly(date.Year, date.Month, 1);
        return date;
    }

    public static string Label(DateOnly date, TimeBucket bucket)
    {
        var start = Start(date, bucket);
        return bucket == TimeBucket.Month
            ? start.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
            : start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Money and percentage arithmetic of the reports: exact decimals, percentages rounded half away from zero to one decimal place, server-side only (section 8.6).</summary>
public static class ReportMath
{
    public static decimal Percent(decimal part, decimal whole) =>
        whole == 0m ? 0m : decimal.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

    /// <summary>The invoice money rule (HO-BIL-001 InvoiceCalculator.Round): two places, midpoint away from zero -- the same mode the CSV writer formats with.</summary>
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
