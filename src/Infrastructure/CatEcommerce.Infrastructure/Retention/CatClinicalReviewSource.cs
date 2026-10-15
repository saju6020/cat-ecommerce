using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>
/// HO-XCT-001 section 8.10 / D-8 -- the Cat Profiles context own count of clinical records past their period. A cat is "past retention" when
/// <c>retention.clinicalYears</c> have passed since the LATER of its death and its last completed appointment; a cat with neither is dated from the
/// day its profile was created. The end of the period is a <b>review flag only</b> (ADR-301): nothing here deletes, anonymises or touches a row, and
/// the report says so: "Eligible for review. Nothing is deleted automatically."
/// </summary>
public sealed class CatClinicalReviewSource(CatEcommerceDbContext db, IBusinessSettings settings, IOperationsClock clock) : IRetentionReviewSource
{
    public const string Key = "cat-clinical";

    public string SourceKey => Key;

    /// <summary>
    /// The date the clinical period is counted from: the LATER of the cat's death and its last completed visit; a cat with neither is dated from the
    /// day its profile was created. All three are main-branch calendar dates (gate fix 2026-10-12, CODE_REVIEW 19). Public for the unit tests.
    /// </summary>
    public static DateOnly SelectAnchor(DateOnly created, DateOnly? deceasedOn, DateOnly? lastCompletedVisit) =>
        deceasedOn is { } died
            ? (lastCompletedVisit is { } visit && visit > died ? visit : died)
            : lastCompletedVisit ?? created;

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var cutoff = asOf.AddYears(-settings.GetInt(SettingKeys.RetentionClinicalYears));
        var zoneId = (await clock.NowAsync(ct)).TimeZoneId;

        // Cheap lower bound in SQL, with a day of slack either side of the UTC/local difference; the exact decision is made on local dates below.
        var slackInstant = new DateTime(cutoff.Year, cutoff.Month, cutoff.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(2);
        var slackDate = cutoff.AddDays(2);
        var candidates = await db.Set<CatAggregate>().AsNoTracking()
            .Where(c => c.DeceasedOn != null ? c.DeceasedOn < slackDate : c.CreatedDate < slackInstant)
            .Select(c => new { c.Id, c.DeceasedOn, c.CreatedDate })
            .ToListAsync(ct);
        if (candidates.Count == 0) return [];

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var lastVisits = await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => candidateIds.Contains(a.CatId) && a.Status == AppointmentStatus.Completed && a.StartsOn != null)
            .GroupBy(a => a.CatId)
            .Select(g => new { CatId = g.Key, Last = g.Max(a => a.StartsOn) })
            .ToDictionaryAsync(g => g.CatId, g => g.Last, ct);

        var anchors = candidates.Select(c =>
        {
            DateOnly? lastVisit = lastVisits.TryGetValue(c.Id, out var last) && last is { } instant ? BranchCalendar.LocalDateOf(instant, zoneId) : null;
            var created = BranchCalendar.LocalDateOf(new DateTimeOffset(DateTime.SpecifyKind(c.CreatedDate, DateTimeKind.Utc)), zoneId);
            return SelectAnchor(created, c.DeceasedOn, lastVisit);
        }).Where(anchor => anchor < cutoff).ToList();

        return anchors.Count == 0
            ? []
            : [new RetentionReviewCount(RecordClass.Clinical, "cat-profile", anchors.Count, anchors.Min())];
    }
}
