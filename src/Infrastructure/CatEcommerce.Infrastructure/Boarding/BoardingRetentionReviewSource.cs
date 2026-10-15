using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-XCT-001 section 5.2 C-ALL-3 / section 8.10 -- how many Boarding records are past their retention period. A REVIEW count only: nothing is ever deleted or anonymised automatically (ADR-301).
/// Financial (D-9, <c>retention.financialYears</c>): ended stays and daycare visits, dated by their last day. Clinical (D-8, <c>retention.clinicalYears</c>): care observations, tasks and incidents,
/// dated by the day they were recorded (Boarding has no per-cat "last visit" -- the record date is the anchor, recorded in the build notes).
/// </summary>
public sealed class BoardingRetentionReviewSource(CatEcommerceDbContext db, IBusinessSettings settings) : IRetentionReviewSource
{
    public string SourceKey => "boarding-records";

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var financialCutoff = asOf.AddYears(-settings.GetInt(SettingKeys.RetentionFinancialYears));
        var clinicalCutoff = asOf.AddYears(-settings.GetInt(SettingKeys.RetentionClinicalYears));
        var counts = new List<RetentionReviewCount>();

        var stays = db.BoardingStays.AsNoTracking().Where(s => s.DepartureOn <= financialCutoff
            && (s.Status == BoardingStayStatus.CheckedOut || s.Status == BoardingStayStatus.Cancelled || s.Status == BoardingStayStatus.NoShow));
        await AddAsync(counts, RecordClass.Financial, "stay", stays.Select(s => s.DepartureOn), ct);

        await AddAsync(counts, RecordClass.Financial, "daycare_visit", db.DaycareVisits.AsNoTracking().Where(v => v.LocalDate <= financialCutoff).Select(v => v.LocalDate), ct);
        await AddAsync(counts, RecordClass.Clinical, "care_observation", db.BoardingCareObservations.AsNoTracking().Where(o => o.LocalDate <= clinicalCutoff).Select(o => o.LocalDate), ct);
        await AddAsync(counts, RecordClass.Clinical, "care_task", db.BoardingCareTasks.AsNoTracking().Where(t => t.LocalDate <= clinicalCutoff).Select(t => t.LocalDate), ct);

        var oldIncidents = db.BoardingIncidents.AsNoTracking().Where(i => i.OccurredOn <= new DateTimeOffset(clinicalCutoff.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)).Select(i => i.OccurredOn);
        var incidentCount = await oldIncidents.CountAsync(ct);
        if (incidentCount > 0) counts.Add(new RetentionReviewCount(RecordClass.Clinical, "incident", incidentCount, DateOnly.FromDateTime((await oldIncidents.MinAsync(ct)).UtcDateTime)));

        return counts;
    }

    private static async Task AddAsync(List<RetentionReviewCount> counts, RecordClass recordClass, string kind, IQueryable<DateOnly> dates, CancellationToken ct)
    {
        var count = await dates.CountAsync(ct);
        if (count > 0) counts.Add(new RetentionReviewCount(recordClass, kind, count, await dates.MinAsync(ct)));
    }
}
