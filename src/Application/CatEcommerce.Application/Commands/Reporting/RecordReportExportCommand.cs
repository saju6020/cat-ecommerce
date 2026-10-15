using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Reporting;

/// <summary>
/// HO-RPT-001 section 11.1 (C1) -- recorded by the export endpoints <b>before</b> a byte is streamed (ADR-285). Carries the filters, the row count and the visible column keys -- never row data.
/// A failed audit write fails the export: an unaudited financial export is worse than a retry.
/// </summary>
public sealed class RecordReportExportCommand : Command
{
    public required string ReportKey { get; init; }
    public required ReportFormat Format { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>The branch filter as the caller gave it: a branch id, <c>all</c>, or null for the active branch.</summary>
    public string? BranchId { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public required int RowCount { get; init; }
    public required IReadOnlyList<string> ColumnKeys { get; init; }
    public string? IpAddress { get; init; }
}
