using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 12.6 Q3. Built from <see cref="RecordClassification"/> and the current settings: no database read beyond settings, so lowering
/// <c>retention.clinicalYears</c> shows immediately.
/// </summary>
public sealed class GetRetentionScheduleQueryHandler(IBusinessSettings settings) : IQueryHandlerAsync<GetRetentionScheduleQuery, RetentionScheduleViewModel>
{
    public Task<QueryResponse<RetentionScheduleViewModel>> HandleAsync(GetRetentionScheduleQuery query)
    {
        var byClass = RecordClassification.All.GroupBy(t => t.RecordClass.Value).ToDictionary(g => g.Key, g => g.ToList());

        var classes = Enumeration.GetAll<RecordClass>().OrderBy(c => c.Value).Select(recordClass =>
        {
            var tables = byClass.GetValueOrDefault(recordClass.Value) ?? [];
            var (period, unit, action) = Describe(recordClass);
            return new RetentionScheduleClassViewModel(
                recordClass.DisplayName,
                tables.Select(t => t.HardDeletePolicy.DisplayName).Distinct().Order(StringComparer.Ordinal).ToList(),
                tables.Select(t => t.Basis).Distinct().Order(StringComparer.Ordinal).ToList(),
                period, unit, action, tables.Count,
                tables.OrderBy(t => t.Schema, StringComparer.Ordinal).ThenBy(t => t.Table, StringComparer.Ordinal)
                    .Select(t => new RetentionScheduleTableViewModel(t.Schema, t.Table, t.HardDeletePolicy.DisplayName, t.Basis)).ToList());
        }).ToList();

        return Task.FromResult(new QueryResponse<RetentionScheduleViewModel> { Result = new RetentionScheduleViewModel(classes) });
    }

    /// <summary>Period, its unit and the action when it ends (ADR-301: a clinical, financial, regulatory or audit record is never deleted at the end of its period).</summary>
    private (int? Period, string? Unit, string Action) Describe(RecordClass recordClass)
    {
        if (recordClass == RecordClass.Clinical) return (settings.GetInt(SettingKeys.RetentionClinicalYears), "years", "ReviewOnly");
        if (recordClass == RecordClass.Financial) return (settings.GetInt(SettingKeys.RetentionFinancialYears), "years", "ReviewOnly");
        // ADR-301: the end of a regulatory period is a review flag only; only HELD personal data inside it (an acceptance IP) is blanked when its hold ends.
        if (recordClass == RecordClass.Regulatory) return (settings.GetInt(SettingKeys.RetentionFinancialYears), "years", "ReviewOnly");
        if (recordClass == RecordClass.Notification) return (settings.GetInt(SettingKeys.RetentionNotificationContentMonths), "months", "Redact");
        if (recordClass == RecordClass.Security) return (settings.GetInt(SettingKeys.RetentionSecurityArtefactDays), "days", "Purge");
        if (recordClass == RecordClass.Media) return (settings.GetInt(SettingKeys.RetentionBoardingMediaMonths), "months", "Purge");
        if (recordClass == RecordClass.Telemetry) return (settings.GetInt(SettingKeys.RetentionWebVitalsDays), "days", "Purge");
        if (recordClass == RecordClass.CustomerPersonal) return (null, null, "AnonymiseOnRequest");
        return (null, null, "Keep");
    }
}

/// <summary>Q4 list -- explicit projection, <c>AsNoTracking</c>, paged.</summary>
public sealed class GetRetentionRunsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetRetentionRunsQuery, PagedResult<RetentionRunSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<RetentionRunSummaryViewModel>>> HandleAsync(GetRetentionRunsQuery query)
    {
        var runs = db.RetentionRuns.AsNoTracking();
        var totalCount = await runs.CountAsync();

        var rows = await runs
            .OrderByDescending(r => r.RequestedOn).ThenBy(r => r.Id)
            .Skip(query.Skip).Take(query.Take)
            .Select(r => new
            {
                r.Id, r.Trigger, r.Status, r.RequestedOn, r.StartedOn, r.FinishedOn,
                PassCount = r.Passes.Count, TotalAffected = r.Passes.Sum(p => (int?)p.Affected) ?? 0, TotalFailed = r.Passes.Sum(p => (int?)p.Failed) ?? 0,
            })
            .ToListAsync();

        var items = rows.Select(r => new RetentionRunSummaryViewModel(
            r.Id, r.Trigger.DisplayName, r.Status.DisplayName, r.RequestedOn, r.StartedOn, r.FinishedOn, r.PassCount, r.TotalAffected, r.TotalFailed)).ToList();

        return new QueryResponse<PagedResult<RetentionRunSummaryViewModel>> { Result = new PagedResult<RetentionRunSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

/// <summary>Q4 detail -- the run with every pass.</summary>
public sealed class GetRetentionRunQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRetentionRunQuery, RetentionRunViewModel?>
{
    public async Task<QueryResponse<RetentionRunViewModel?>> HandleAsync(GetRetentionRunQuery query)
    {
        var row = await db.RetentionRuns.AsNoTracking()
            .Where(r => r.Id == query.RetentionRunId)
            .Select(r => new
            {
                r.Id, r.Trigger, r.Status, r.RequestedOn, r.StartedOn, r.FinishedOn,
                Passes = r.Passes.Select(p => new { p.Kind, p.Cutoff, p.Examined, p.Affected, p.Held, p.Failed, p.ErrorCodes, p.DurationMs }).ToList(),
            })
            .FirstOrDefaultAsync();

        RetentionRunViewModel? result = row is null ? null : new RetentionRunViewModel(
            row.Id, row.Trigger.DisplayName, row.Status.DisplayName, row.RequestedOn, row.StartedOn, row.FinishedOn,
            row.Passes.OrderBy(p => p.Kind.Value)
                .Select(p => new RetentionPassViewModel(p.Kind.DisplayName, p.Cutoff, p.Examined, p.Affected, p.Held, p.Failed, p.ErrorCodes, p.DurationMs)).ToList());

        return new QueryResponse<RetentionRunViewModel?> { Result = result };
    }
}

/// <summary>Q5 -- calls every registered <see cref="IRetentionReviewSource"/>. A failing source propagates: reporting it as zero rows would be a false all-clear.</summary>
public sealed class GetRetentionReviewQueryHandler(IEnumerable<IRetentionReviewSource> sources, IOperationsClock clock)
    : IQueryHandlerAsync<GetRetentionReviewQuery, RetentionReviewViewModel>
{
    public const string Note = "Eligible for review. Nothing is deleted automatically.";

    public async Task<QueryResponse<RetentionReviewViewModel>> HandleAsync(GetRetentionReviewQuery query)
    {
        var today = (await clock.NowAsync(CancellationToken.None)).LocalDate;
        var rows = new List<RetentionReviewRowViewModel>();

        foreach (var source in sources.OrderBy(s => s.SourceKey, StringComparer.Ordinal))
            foreach (var count in await source.CountPastRetentionAsync(today, CancellationToken.None))
                rows.Add(new RetentionReviewRowViewModel(count.RecordClass.DisplayName, count.RecordKind, count.Count, count.OldestAnchor, source.SourceKey));

        return new QueryResponse<RetentionReviewViewModel> { Result = new RetentionReviewViewModel(today, rows, Note) };
    }
}
