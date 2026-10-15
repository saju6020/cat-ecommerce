using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Reporting;

/// <summary>
/// HO-RPT-001 section 12.4 -- the catalogue filtered to the caller: only the categories the caller holds, and only the columns they may see (a restricted column is absent, not blanked).
/// <c>defaultRange</c> is each report's default period resolved server-side for the active branch, so the client never computes dates. A report whose source is not registered is absent.
/// </summary>
public sealed class GetReportCatalogueQueryHandler(
    IReportCatalogue catalogue, IEnumerable<IReportSource> sources, IReportingReadScope scope, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetReportCatalogueQuery, IReadOnlyList<ReportCatalogueEntryViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<ReportCatalogueEntryViewModel>>> HandleAsync(GetReportCatalogueQuery query)
    {
        var zone = await scope.RunAsync(async (db, ct) =>
        {
            var branchId = query.ActiveBranchId ?? AdministrationSeed.MainBranchId;
            var zoneId = await db.Set<RvBranchRow>().AsNoTracking().Where(b => b.BranchId == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct);
            return ResolveZone(zoneId);
        }, CancellationToken.None);

        var now = timeProvider.GetUtcNow();
        var registered = sources.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var canAll = ReportAccessPolicy.CanUseAllBranches(query.CallerRoles);
        var canSchedule = ReportAccessPolicy.Holds(query.CallerRoles, Actions.Reports.Schedule);

        var entries = catalogue.All
            .Where(d => registered.Contains(d.Key) && ReportAccessPolicy.CanOpen(d, query.CallerRoles))
            .Select(d =>
            {
                var range = ReportPeriodResolver.Resolve(d.DefaultPeriod, now, zone);
                return new ReportCatalogueEntryViewModel(
                    d.Key, d.Title, d.Description, d.Category.DisplayName, d.Kind.DisplayName, d.TimeBucketed, d.DefaultPeriod.DisplayName,
                    new ReportDateRangeViewModel(range.From, range.To), d.MaxRangeDays, ["Csv", "Pdf"],
                    d.Parameters.Select(p => new ReportParameterViewModel(p.Name, p.Label, p.Type.DisplayName, p.AllowedValues, p.Min, p.Max, p.Default)).ToList(),
                    ReportAccessPolicy.VisibleColumns(d, query.CallerRoles.ToList())
                        .Select(c => new ReportColumnViewModel(c.Key, c.Label, c.Type.DisplayName, c.Unit, c.Sortable, c.Description)).ToList(),
                    canAll, canSchedule);
            })
            .ToList();

        return new QueryResponse<IReadOnlyList<ReportCatalogueEntryViewModel>> { Result = entries };
    }

    private static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? BillingRules.DefaultTimeZoneId : timeZoneId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(BillingRules.DefaultTimeZoneId); }
    }
}
