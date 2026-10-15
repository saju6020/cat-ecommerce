using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>
/// HO-PHM-001 / HO-XCT-001 §5.2 C-ALL-3 (D-8) -- Pharmacy's own count of CLINICAL records past their retention period: prescriptions, dispensings and diet
/// authorisations of cats whose last visit or death (whichever is later) is more than <see cref="ClinicalNoteRules.DefaultClinicalRetentionYears"/> ago (the assumed
/// default until HO-XCT-001 registers <c>retention.clinicalYears</c>, SRS Q-1). A review flag only: nothing is deleted or anonymised (ADR-301). The controlled-drug
/// register is Regulatory and is deliberately not counted here -- an inspector's record is never offered for review by this slice.
/// </summary>
public sealed class PharmacyRetentionReviewSource(CatEcommerceDbContext db) : IRetentionReviewSource
{
    public string SourceKey => "pharmacy-clinical";

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var cutoff = asOf.AddYears(-ClinicalNoteRules.DefaultClinicalRetentionYears);

        var lastVisits = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => !v.IsMarkedToDelete)
            .GroupBy(v => v.CatId)
            .Select(g => new { CatId = g.Key, Last = g.Max(v => v.OpenedOn) })
            .ToListAsync(ct);
        if (lastVisits.Count == 0) return [];

        var deceased = (await db.Set<CatAggregate>().AsNoTracking().Where(c => c.DeceasedOn != null).Select(c => new { c.Id, c.DeceasedOn }).ToListAsync(ct))
            .ToDictionary(c => c.Id, c => c.DeceasedOn!.Value);

        var past = lastVisits
            .Select(v =>
            {
                var lastVisit = DateOnly.FromDateTime(v.Last.UtcDateTime);
                return new { v.CatId, Anchor = deceased.TryGetValue(v.CatId, out var died) && died > lastVisit ? died : lastVisit };
            })
            .Where(v => v.Anchor < cutoff)
            .ToList();
        if (past.Count == 0) return [];

        var catIds = past.Select(p => p.CatId).ToList();
        var oldest = past.Min(p => p.Anchor);

        var prescriptions = await db.Prescriptions.AsNoTracking().CountAsync(p => catIds.Contains(p.CatId), ct);
        var dispensings = await db.Dispensings.AsNoTracking().CountAsync(d => catIds.Contains(d.CatId), ct);
        var diets = await db.DietAuthorisations.AsNoTracking().CountAsync(a => catIds.Contains(a.CatId), ct);

        var results = new List<RetentionReviewCount>();
        if (prescriptions > 0) results.Add(new RetentionReviewCount(RecordClass.Clinical, "prescription", prescriptions, oldest));
        if (dispensings > 0) results.Add(new RetentionReviewCount(RecordClass.Clinical, "dispensing", dispensings, oldest));
        if (diets > 0) results.Add(new RetentionReviewCount(RecordClass.Clinical, "diet-authorisation", diets, oldest));
        return results;
    }
}
