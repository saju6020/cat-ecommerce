using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Dashboard;

/// <summary>
/// HO-RPT-001 section 8.11 (US-RPT-05, ADR-286) -- the role dashboard, composed on the server. The tile set is the union over the caller's roles; each tile is computed only if the caller holds its gate
/// action (the source is not even invoked otherwise, U-E2). Values are live, through <see cref="IReportingReadScope"/>, with no cache; a tile with no source (its module not built) is absent, not zero (U-E4).
/// </summary>
public sealed class GetDashboardQueryHandler(
    IEnumerable<IDashboardTileSource> sources, IReportingReadScope scope, IBusinessSettings settings, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetDashboardQuery, DashboardOutcome>
{
    public async Task<QueryResponse<DashboardOutcome>> HandleAsync(GetDashboardQuery query)
    {
        if (query.ActiveBranchId is not { } branchId)
            return new QueryResponse<DashboardOutcome> { Result = new DashboardOutcome(null, BusinessViolationCodes.BranchNotAssigned) };

        var bySource = sources.GroupBy(s => s.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal); // the last registration wins (a test host decorates a tile)
        var now = timeProvider.GetUtcNow();

        var view = await scope.RunAsync(async (db, ct) =>
        {
            var branch = await db.Set<RvBranchRow>().AsNoTracking().Where(b => b.BranchId == branchId).FirstOrDefaultAsync(ct);
            var zone = ResolveZone(branch?.TimeZoneId);
            var context = new DashboardContext(db, branchId, zone, now, query.CallerId, settings, settings.GetString(SettingKeys.CommerceCurrencyCode, branchId));

            var tiles = new List<DashboardTileViewModel>();
            foreach (var definition in DashboardTiles.ForRoles(query.CallerRoles))
            {
                var allowed = ReportAccessPolicy.Holds(query.CallerRoles, definition.GateAction)
                              || (definition.AlternativeGateActions?.Any(a => ReportAccessPolicy.Holds(query.CallerRoles, a)) ?? false);
                if (!allowed || !bySource.TryGetValue(definition.Key, out var source)) continue;

                var result = await source.RunAsync(context, ct);
                tiles.Add(new DashboardTileViewModel(
                    definition.Key, definition.Title, result.Figures.Select(ToViewModel).ToList(), DrillDown(definition, result, query.CallerRoles, context.Today), result.Note));
            }

            return new DashboardViewModel(
                branchId, branch?.Name ?? string.Empty, context.Today, zone.Id, now, settings.GetInt(SettingKeys.ReportingDashboardRefreshSeconds), tiles);
        }, CancellationToken.None);

        return new QueryResponse<DashboardOutcome> { Result = new DashboardOutcome(view, null) };
    }

    private static DashboardFigureViewModel ToViewModel(DashboardFigure figure) => new(
        figure.Key, figure.Label, figure.Value is ReportMoney money ? ReportResultFormatter.Money(money) : figure.Value, figure.Type, figure.Unit);

    /// <summary>A drill-down only when the caller may open that report; otherwise null.</summary>
    private static DashboardDrillDownViewModel? DrillDown(DashboardTileDefinition definition, DashboardTileResult result, IReadOnlyList<string> roles, DateOnly today)
    {
        if (definition.DrillReportKey is not { } key || ReportCatalogue.Find(key) is not { } report || !ReportAccessPolicy.CanOpen(report, roles)) return null;
        return new DashboardDrillDownViewModel(key, result.DrillFrom ?? today, result.DrillTo ?? today, new Dictionary<string, string>());
    }

    private static TimeZoneInfo ResolveZone(string? id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? BillingRules.DefaultTimeZoneId : id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(BillingRules.DefaultTimeZoneId); }
    }
}
