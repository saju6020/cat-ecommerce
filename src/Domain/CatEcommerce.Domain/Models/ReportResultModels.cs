using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Models;

// HO-RPT-001 section 8.12 / 12.4 -- the raw report model: rows hold typed CLR values (string, long, decimal, ReportMoney, DateOnly, DateTimeOffset, bool or null). The screen, the CSV and the PDF
// are all produced from one ReportResult (ADR-285). Lives in Domain.Models so the Read pipeline and the Infrastructure renderers can share it.

/// <summary>A money cell. Amount is exact (<c>decimal</c>) and is never summed across currencies.</summary>
public sealed record ReportMoney(decimal Amount, string CurrencyCode);

/// <summary>A row of a report: column key to a typed value (string, long, decimal, <see cref="ReportMoney"/>, <see cref="DateOnly"/>, <see cref="DateTimeOffset"/>, bool or null).</summary>
public sealed class ReportRow : Dictionary<string, object?>
{
    public ReportRow() : base(StringComparer.Ordinal) { }
}

public sealed record ReportNote(string Code, string Text);

/// <summary>How much of the revenue a margin figure covers (ADR-284). <c>Percent</c> is one decimal place, half away from zero.</summary>
public sealed record ReportCoverage(ReportMoney CostedRevenue, ReportMoney TotalRevenue, decimal Percent);

public sealed record ReportFilters(
    DateOnly From, DateOnly To, Guid? BranchId, string BranchName, string TimeZoneId, string? GroupBy, IReadOnlyDictionary<string, object> Parameters);

public sealed record ReportResult(
    string ReportKey, string Title, DateTimeOffset GeneratedOn, string GeneratedBy, ReportFilters Filters, IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows, ReportRow? Totals, IReadOnlyList<ReportNote> Notes, ReportCoverage? Coverage,
    int PageNumber, int PageSize, int TotalCount);

