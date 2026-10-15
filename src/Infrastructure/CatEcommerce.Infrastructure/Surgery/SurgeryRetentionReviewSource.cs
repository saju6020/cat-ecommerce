using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Surgery;

/// <summary>
/// HO-SRG-001 / HO-XCT-001 section 5.2 C-ALL-3 (D-8) -- Surgery's own count of CLINICAL records past their retention period: surgeries, anaesthesia log entries and surgical reports
/// of cats whose last visit, last surgery or death (whichever is latest) is more than <see cref="ClinicalNoteRules.DefaultClinicalRetentionYears"/> ago (the assumed default until
/// HO-XCT-001 registers <c>retention.clinicalYears</c>, SRS Q-1). The last SURGERY counts as an anchor because the visit a surgery belongs to is opened by HO-EMR-001 (contract E2),
/// which may not have done so. A review flag only: nothing is deleted or anonymised (ADR-301). The controlled-drug register is Regulatory and belongs to Pharmacy.
/// </summary>
public sealed class SurgeryRetentionReviewSource(CatEcommerceDbContext db) : IRetentionReviewSource
{
    public string SourceKey => "surgery-clinical";

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var cutoff = asOf.AddYears(-ClinicalNoteRules.DefaultClinicalRetentionYears);
        var cutoffInstant = new DateTimeOffset(cutoff.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Only cats that have surgery records matter; the anchor of each is the latest of its last visit, last surgery and death.
        var lastSurgeries = await db.Surgeries.AsNoTracking().Where(s => !s.IsMarkedToDelete)
            .GroupBy(s => s.CatId).Select(g => new { CatId = g.Key, Last = g.Max(s => s.StartsOn) }).ToListAsync(ct);
        if (lastSurgeries.Count == 0) return [];

        var catIds = lastSurgeries.Select(s => s.CatId).ToList();
        var lastVisits = await db.Set<VisitAggregate>().AsNoTracking().Where(v => !v.IsMarkedToDelete && catIds.Contains(v.CatId))
            .GroupBy(v => v.CatId).Select(g => new { CatId = g.Key, Last = g.Max(v => v.OpenedOn) }).ToListAsync(ct);
        var deceased = (await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id) && c.DeceasedOn != null).Select(c => new { c.Id, c.DeceasedOn }).ToListAsync(ct))
            .ToDictionary(c => c.Id, c => c.DeceasedOn!.Value);
        var visitByCat = lastVisits.ToDictionary(v => v.CatId, v => DateOnly.FromDateTime(v.Last.UtcDateTime));

        var past = lastSurgeries
            .Select(s =>
            {
                var anchor = DateOnly.FromDateTime(s.Last.UtcDateTime);
                if (visitByCat.TryGetValue(s.CatId, out var visit) && visit > anchor) anchor = visit;
                if (deceased.TryGetValue(s.CatId, out var died) && died > anchor) anchor = died;
                return new { s.CatId, Anchor = anchor };
            })
            .Where(x => x.Anchor < cutoff)
            .ToList();
        if (past.Count == 0) return [];

        var pastCats = past.Select(p => p.CatId).ToList();
        var oldest = past.Min(p => p.Anchor);

        var surgeries = await db.Surgeries.AsNoTracking().CountAsync(s => !s.IsMarkedToDelete && pastCats.Contains(s.CatId), ct);
        var logEntries = await db.AnaesthesiaLogEntries.AsNoTracking().CountAsync(e => !e.IsMarkedToDelete && pastCats.Contains(e.CatId), ct);
        var reports = await db.SurgicalReports.AsNoTracking().CountAsync(r => !r.IsMarkedToDelete && pastCats.Contains(r.CatId), ct);

        var results = new List<RetentionReviewCount>();
        if (surgeries > 0) results.Add(new RetentionReviewCount(RecordClass.Clinical, "surgery", surgeries, oldest));
        if (logEntries > 0) results.Add(new RetentionReviewCount(RecordClass.Clinical, "anaesthesia-log-entry", logEntries, oldest));
        if (reports > 0) results.Add(new RetentionReviewCount(RecordClass.Clinical, "surgical-report", reports, oldest));
        return results;
    }
}
