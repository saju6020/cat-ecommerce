using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Reporting;

// HO-RPT-001 section 12.6 -- viewing a report is a query: it returns data and never writes. Every figure comes from the report.rv_* views inside IReportingReadScope (ADR-278, ADR-279).

/// <summary>
/// What a scheduled run is allowed to see, built by the worker from the intersection of every recipient's actions (ADR-288) and the schedule's branch -- never from an unrestricted
/// system identity (section 8.9 step 4).
/// </summary>
public sealed record ReportAccess(IReadOnlyCollection<string> VisibleColumnKeys, Guid? BranchId, bool AllBranches);

/// <summary>
/// Runs one report. The handler validates the filters, resolves the branch scope from the caller (or takes the worker's <see cref="ReportAccess"/>), runs the one
/// <c>IReportSource</c> for the key inside the read-only scope, removes the columns the caller may not see, then sorts and pages. In export mode paging is replaced by the format cap plus one row.
/// </summary>
public sealed class RunReportQuery : Query<ReportOutcome>
{
    public required string ReportKey { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>The raw <c>branchId</c> filter: empty (the active branch), <c>all</c>, or a branch id.</summary>
    public string? BranchId { get; init; }

    /// <summary>The caller's active branch from <c>X-Branch-Id</c> (already validated against the assignment by HO-ADM-001's accessor).</summary>
    public Guid? ActiveBranchId { get; init; }

    public string? GroupBy { get; init; }
    public string? Sort { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    /// <summary>Null on screen; the format when the result is being exported.</summary>
    public ReportFormat? ExportFormat { get; init; }

    public required IReadOnlyList<string> CallerRoles { get; init; }

    /// <summary>The caller's staff account id (their branch assignment is read from it).</summary>
    public required Guid CallerId { get; init; }

    /// <summary>The worker's access. When set, the caller's roles are not consulted for columns or branch.</summary>
    public ReportAccess? AccessOverride { get; init; }

    /// <summary>The instant the "as of" and "generated on" values use; null = now.</summary>
    public DateTimeOffset? AsOf { get; init; }
}

/// <summary>The catalogue filtered to the caller, with each report's default range resolved server-side for the active branch (the client never computes dates).</summary>
public sealed class GetReportCatalogueQuery : Query<IReadOnlyList<ReportCatalogueEntryViewModel>>
{
    public required IReadOnlyList<string> CallerRoles { get; init; }
    public Guid? ActiveBranchId { get; init; }
}

/// <summary>The home dashboard (section 8.11): today at the caller's active branch, one tile per entry of DashboardTiles the caller's roles name and whose gate action the caller holds.</summary>
public sealed class GetDashboardQuery : Query<DashboardOutcome>
{
    public required IReadOnlyList<string> CallerRoles { get; init; }
    public required Guid CallerId { get; init; }
    public Guid? ActiveBranchId { get; init; }
}
