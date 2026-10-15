using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Operations;

/// <summary>
/// HO-XCT-001 section 12.6 Q2 -- the p75 of each budgeted web vital per route and viewport over a date range (at most
/// <c>WebVitalBudgets.MaxRangeDays</c> days), judged against the budgets. <c>RouteTemplate</c> and <c>ViewportClass</c> narrow it.
/// </summary>
public sealed class GetWebVitalsSummaryQuery : Query<WebVitalsSummaryViewModel>
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public string? RouteTemplate { get; init; }

    /// <summary>A <c>ViewportClass</c> display name.</summary>
    public string? ViewportClass { get; init; }
}
