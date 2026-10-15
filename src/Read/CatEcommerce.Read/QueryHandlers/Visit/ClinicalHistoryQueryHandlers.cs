using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ClinicalHistory;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.QueryHandlers.Cat;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Visit;

/// <summary>
/// §12.4 <c>GET /api/staff/cats/{catId}/clinical-history</c> (US-CLN-13, NFR-PRF-4). One summary call composed from the
/// registered section providers plus a handful of small projections: active problems, the last five visits, the last
/// vitals, the whole weight series (points only), the vaccination status (HO-CAT-001's calculator), pending
/// diagnostic orders, and per declared section <c>{ key, available, owner, count }</c>. Server budget: 600 ms p95
/// (ADR-128) -- every read below is <c>AsNoTracking</c>, projected before materialising and index-served (§12.1).
/// The calls are sequential on purpose: one scoped <c>DbContext</c> is not safe for concurrent use.
/// </summary>
public sealed class GetClinicalHistorySummaryQueryHandler(
    CatEcommerceDbContext db,
    IEnumerable<IClinicalHistorySectionProvider> providers,
    VisitsSectionProvider visits,
    IBusinessSettings settings,
    TimeProvider timeProvider,
    ICatRecordClock catClock)
    : IQueryHandlerAsync<GetClinicalHistorySummaryQuery, ClinicalHistorySummaryViewModel?>
{
    private static readonly TimeSpan ActiveProblemWindow = TimeSpan.FromDays(365);

    public async Task<QueryResponse<ClinicalHistorySummaryViewModel?>> HandleAsync(GetClinicalHistorySummaryQuery query)
    {
        var catId = query.CatId;
        var cat = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == catId && !c.IsMarkedToDelete)
            .Select(c => new { c.Id, c.Name, c.Sex, c.DateOfBirth, c.Breed, c.IsHighRisk, c.LifeStatus })
            .FirstOrDefaultAsync();
        if (cat is null) return new QueryResponse<ClinicalHistorySummaryViewModel?> { Result = null };

        var now = timeProvider.GetUtcNow();
        var since = now - ActiveProblemWindow;

        var problemRows = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.CatId == catId && v.OpenedOn >= since && !v.IsMarkedToDelete)
            .SelectMany(v => v.Problems.Where(p => p.WithdrawnOn == null).Select(p => new { p.DiagnosisCodeId, p.Code, p.Name, p.Certainty, v.OpenedOn }))
            .ToListAsync();
        var activeProblems = problemRows
            .GroupBy(r => r.DiagnosisCodeId)
            .Select(g => g.OrderByDescending(r => r.OpenedOn).First())
            .OrderByDescending(r => r.OpenedOn)
            .Select(r => new ClinicalHistoryProblemViewModel(r.DiagnosisCodeId, r.Code, r.Name, r.Certainty.DisplayName, r.OpenedOn))
            .ToList();

        var recentVisits = await visits.ReadAsync(catId, 0, 5, default);

        var lastVitals = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.CatId == catId && !v.IsMarkedToDelete)
            .SelectMany(v => v.Vitals)
            .OrderByDescending(e => e.MeasuredOn).ThenByDescending(e => e.RecordedOn)
            .Select(VitalsRow.From)
            .FirstOrDefaultAsync();
        var vitalsNames = await StaffNames.LookupAsync(db, [lastVitals?.RecordedBy]);

        var weights = await db.CatWeightRecords.AsNoTracking()
            .Where(w => w.CatId == catId)
            .OrderBy(w => w.RecordedOn)
            .Select(w => new CatWeightPointViewModel(w.RecordedOn, w.Weight.Kilograms))
            .ToListAsync();

        var pending = await db.Set<DiagnosticOrderAggregate>().AsNoTracking()
            .Where(o => o.CatId == catId && o.Status == DiagnosticOrderStatus.Pending && !o.IsMarkedToDelete)
            .OrderBy(o => o.OrderedOn)
            .Select(o => new { o.Id, o.VisitId, o.CatId, o.Kind, o.Status, o.OrderedOn, o.OrderedBy })
            .Take(50)
            .ToListAsync();
        var orderNames = await StaffNames.LookupAsync(db, pending.Select(o => (Guid?)o.OrderedBy));

        var sections = new List<ClinicalHistorySectionSummary>(ClinicalHistorySections.All.Count);
        var byKey = providers.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First());
        foreach (var section in ClinicalHistorySections.All)
        {
            sections.Add(byKey.TryGetValue(section.Key, out var provider)
                ? await provider.GetSummaryAsync(catId, default)
                : new ClinicalHistorySectionSummary(section.Key, false, section.Owner, 0));
        }

        var summary = new ClinicalHistorySummaryViewModel(
            new ClinicalHistoryCatViewModel(cat.Id, cat.Name, cat.Sex.DisplayName, cat.DateOfBirth, cat.Breed, cat.IsHighRisk, cat.LifeStatus.DisplayName),
            activeProblems,
            recentVisits,
            lastVitals is null ? null : GetVisitDetailQueryHandler.ToViewModel(lastVitals, vitalsNames),
            weights,
            await ReadVaccinationStatusAsync(catId),
            pending.Select(o => new DiagnosticOrderListItemViewModel(o.Id, o.VisitId, o.CatId, cat.Name, o.Kind.DisplayName, o.Status.DisplayName, o.OrderedOn, StaffNames.Of(orderNames, o.OrderedBy))).ToList(),
            sections);

        return new QueryResponse<ClinicalHistorySummaryViewModel?> { Result = summary };
    }

    /// <summary>HO-CAT-001's calculator over the same inputs its own query reads (Q4).</summary>
    private async Task<CatVaccinationStatusViewModel?> ReadVaccinationStatusAsync(Guid catId)
    {
        var activeCoreVaccines = await db.Vaccines.AsNoTracking()
            .Where(v => v.IsCore && v.IsActive)
            .Select(v => new ActiveCoreVaccine(v.Id, v.Code, v.Name))
            .ToListAsync();

        var records = await db.Cats.AsNoTracking()
            .Where(c => c.Id == catId)
            .SelectMany(c => c.Vaccinations)
            .Select(v => new CatVaccinationRecord(v.VaccineId, v.AdministeredOn, v.NextDueOn))
            .ToListAsync();

        return CatVaccinationStatusCalculator.Calculate(
            catId, activeCoreVaccines, records, await catClock.LocalTodayAsync(CancellationToken.None), settings.GetInt(SettingKeys.ClinicalVaccinationDueSoonDays));
    }
}

/// <summary>§12.4 <c>GET .../clinical-history/{section}</c> -- dispatches to the section's provider (ADR-128).</summary>
public sealed class GetClinicalHistorySectionQueryHandler(CatEcommerceDbContext db, IEnumerable<IClinicalHistorySectionProvider> providers)
    : IQueryHandlerAsync<GetClinicalHistorySectionQuery, ClinicalHistorySectionViewModel?>
{
    public async Task<QueryResponse<ClinicalHistorySectionViewModel?>> HandleAsync(GetClinicalHistorySectionQuery query)
    {
        var definition = ClinicalHistorySections.Find(query.Section);
        if (definition is null || !await db.Set<CatAggregate>().AsNoTracking().AnyAsync(c => c.Id == query.CatId && !c.IsMarkedToDelete))
            return new QueryResponse<ClinicalHistorySectionViewModel?> { Result = null };

        var provider = providers.FirstOrDefault(p => p.Key == definition.Key);
        if (provider is null)
            return new QueryResponse<ClinicalHistorySectionViewModel?>
            {
                Result = new ClinicalHistorySectionViewModel(definition.Key, false, definition.Owner, [], query.PageNumber, query.PageSize, 0)
            };

        var page = await provider.GetPageAsync(query.CatId, query.PageNumber, query.PageSize, default);
        return new QueryResponse<ClinicalHistorySectionViewModel?>
        {
            Result = new ClinicalHistorySectionViewModel(definition.Key, true, definition.Owner, page.Items, page.PageNumber, page.PageSize, page.TotalCount)
        };
    }
}
