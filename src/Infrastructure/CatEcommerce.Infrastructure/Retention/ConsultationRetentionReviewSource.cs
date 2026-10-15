using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>
/// HO-EMR-001 / HO-XCT-001 §5.2 C-ALL-3 -- Consultation's own count of clinical records past their retention period. Clinical
/// records are kept <c>retention.clinicalYears</c> after the cat's <b>last visit or death, whichever is later</b> (D-8; the
/// assumed default is <see cref="ClinicalNoteRules.DefaultClinicalRetentionYears"/> until HO-XCT-001 registers the setting, SRS
/// Q-1). The end of the period is a <b>review flag only</b> -- nothing here deletes, anonymises or touches a row (ADR-301); no
/// delete path exists for any clinical record (C-ALL-4, ADR-118). Signed consents (Regulatory, NFR-LEG-4) follow the same anchor.
/// </summary>
public sealed class ConsultationRetentionReviewSource(CatEcommerceDbContext db) : IRetentionReviewSource
{
    public string SourceKey => "consultation-clinical";

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var cutoff = asOf.AddYears(-ClinicalNoteRules.DefaultClinicalRetentionYears);

        var lastVisits = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => !v.IsMarkedToDelete)
            .GroupBy(v => v.CatId)
            .Select(g => new { CatId = g.Key, Last = g.Max(v => v.OpenedOn), Visits = g.Count() })
            .ToListAsync(ct);
        if (lastVisits.Count == 0) return [];

        var deceased = await db.Set<CatAggregate>().AsNoTracking()
            .Where(c => c.DeceasedOn != null)
            .Select(c => new { c.Id, c.DeceasedOn })
            .ToDictionaryAsync(c => c.Id, c => c.DeceasedOn!.Value, ct);

        var past = lastVisits
            .Select(v =>
            {
                var lastVisit = DateOnly.FromDateTime(v.Last.UtcDateTime);
                var anchor = deceased.TryGetValue(v.CatId, out var died) && died > lastVisit ? died : lastVisit;
                return new { v.CatId, v.Visits, Anchor = anchor };
            })
            .Where(v => v.Anchor < cutoff)
            .ToList();
        if (past.Count == 0) return [];

        var catIds = past.Select(p => p.CatId).ToList();
        var orders = await db.Set<DiagnosticOrderAggregate>().AsNoTracking().CountAsync(o => catIds.Contains(o.CatId), ct);
        var consents = await db.Set<ConsentRecordAggregate>().AsNoTracking().CountAsync(c => catIds.Contains(c.CatId), ct);
        var oldest = past.Min(p => p.Anchor);

        var results = new List<RetentionReviewCount>
        {
            new(RecordClass.Clinical, "visit", past.Sum(p => p.Visits), oldest),
            new(RecordClass.Clinical, "diagnostic-order", orders, oldest),
        };
        if (consents > 0) results.Add(new RetentionReviewCount(RecordClass.Regulatory, "consent", consents, oldest));

        return results;
    }
}
