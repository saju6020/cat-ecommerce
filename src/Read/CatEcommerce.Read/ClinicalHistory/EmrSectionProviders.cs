using System.Globalization;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.QueryHandlers.Visit;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.ClinicalHistory;

/// <summary>
/// Shared shape of the database-backed providers (§8.7): a count, and a page read as <c>skip/take</c> newest first.
/// <see cref="ReadAsync"/> is public so the <c>timeline</c> provider can merge the heads of the other sources
/// without paging through them one by one.
/// </summary>
public abstract class DbSectionProvider(CatEcommerceDbContext db) : IClinicalHistorySectionProvider
{
    protected CatEcommerceDbContext Db => db;

    public abstract string Key { get; }

    public string Owner => ClinicalHistorySections.Find(Key)?.Owner ?? "";

    public virtual async Task<ClinicalHistorySectionSummary> GetSummaryAsync(Guid catId, CancellationToken ct) =>
        new(Key, true, Owner, await CountAsync(catId, ct));

    public virtual async Task<PagedResult<ClinicalHistoryItem>> GetPageAsync(Guid catId, int pageNumber, int pageSize, CancellationToken ct)
    {
        var total = await CountAsync(catId, ct);
        var skip = (long)(pageNumber - 1) * pageSize;
        var items = skip >= total ? [] : await ReadAsync(catId, (int)skip, pageSize, ct);
        return new PagedResult<ClinicalHistoryItem>(items, pageNumber, pageSize, total);
    }

    public abstract Task<int> CountAsync(Guid catId, CancellationToken ct);

    public abstract Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct);

    protected static DateTimeOffset AtMidnightUtc(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    protected static IReadOnlyDictionary<string, string?> Fields(params (string Name, string? Value)[] fields) =>
        fields.ToDictionary(f => f.Name, f => f.Value);
}

/// <summary><c>visits</c> -- date, service, vet, status, problems, addendum count (§8.7). Served by
/// <c>ix_visit_cat_opened_on</c>.</summary>
public sealed class VisitsSectionProvider(CatEcommerceDbContext db) : DbSectionProvider(db)
{
    public override string Key => ClinicalHistorySections.Visits;

    public override Task<int> CountAsync(Guid catId, CancellationToken ct) =>
        Db.Set<VisitAggregate>().AsNoTracking().CountAsync(v => v.CatId == catId && !v.IsMarkedToDelete, ct);

    public override async Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct)
    {
        var visits = await Db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.CatId == catId && !v.IsMarkedToDelete)
            .OrderByDescending(v => v.OpenedOn).ThenBy(v => v.Id)
            .Skip(skip).Take(take)
            .Select(v => new { v.Id, v.ServiceName, v.OpenedOn, v.Status, v.OpenedBy, v.FinalisedBy })
            .ToListAsync(ct);

        var ids = visits.Select(v => v.Id).ToList();
        var addendumCounts = ids.Count == 0
            ? new Dictionary<Guid, int>()
            : await Db.Set<VisitAddendumAggregate>().AsNoTracking()
                .Where(a => ids.Contains(a.VisitId))
                .GroupBy(a => a.VisitId)
                .Select(g => new { VisitId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.VisitId, g => g.Count, ct);

        var problems = await VisitProblems.ActiveAsync(Db, ids, ct);
        var names = await StaffNames.LookupAsync(Db, visits.Select(v => v.FinalisedBy ?? v.OpenedBy).Select(id => (Guid?)id), ct);

        return visits.Select(v => new ClinicalHistoryItem(
            v.Id, "visit", v.OpenedOn, v.ServiceName,
            string.Join(", ", problems[v.Id].Select(p => p.Name)),
            Fields(
                ("status", v.Status.DisplayName),
                ("vet", StaffNames.Of(names, v.FinalisedBy ?? v.OpenedBy)),
                ("addendumCount", (addendumCounts.TryGetValue(v.Id, out var count) ? count : 0).ToString(CultureInfo.InvariantCulture))))).ToList();
    }
}

/// <summary><c>diagnoses</c> -- the distinct problems across <b>finalised</b> visits, with first and last seen and the latest
/// certainty (§8.7). A cat has a handful of visits a year, so the problem rows are read once and grouped in memory.</summary>
public sealed class DiagnosesSectionProvider(CatEcommerceDbContext db) : DbSectionProvider(db)
{
    public override string Key => ClinicalHistorySections.Diagnoses;

    private sealed record Row(Guid DiagnosisCodeId, string Code, string Name, ProblemCertainty Certainty, DateTimeOffset OpenedOn);

    private async Task<List<ClinicalHistoryItem>> BuildAsync(Guid catId, CancellationToken ct)
    {
        var rows = await Db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.CatId == catId && v.Status == VisitStatus.Finalised && !v.IsMarkedToDelete)
            .SelectMany(v => v.Problems.Where(p => p.WithdrawnOn == null).Select(p => new { p.DiagnosisCodeId, p.Code, p.Name, p.Certainty, v.OpenedOn }))
            .ToListAsync(ct);

        return rows
            .Select(r => new Row(r.DiagnosisCodeId, r.Code, r.Name, r.Certainty, r.OpenedOn))
            .GroupBy(r => r.DiagnosisCodeId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.OpenedOn).First();
                var first = g.Min(r => r.OpenedOn);
                return new ClinicalHistoryItem(
                    g.Key, "diagnosis", latest.OpenedOn, latest.Name, null,
                    Fields(
                        ("code", latest.Code),
                        ("certainty", latest.Certainty.DisplayName),
                        ("firstSeen", first.ToString("O", CultureInfo.InvariantCulture)),
                        ("lastSeen", latest.OpenedOn.ToString("O", CultureInfo.InvariantCulture)),
                        ("occurrences", g.Count().ToString(CultureInfo.InvariantCulture))));
            })
            .OrderByDescending(i => i.OccurredOn).ThenBy(i => i.Title, StringComparer.Ordinal)
            .ToList();
    }

    public override async Task<int> CountAsync(Guid catId, CancellationToken ct) => (await BuildAsync(catId, ct)).Count;

    public override async Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct) =>
        (await BuildAsync(catId, ct)).Skip(skip).Take(take).ToList();
}

/// <summary><c>lab-results</c> -- orders with their latest result set and the flags it carries (§8.7). Served by
/// <c>ix_diagnostic_order_cat_ordered_on</c>.</summary>
public sealed class LabResultsSectionProvider(CatEcommerceDbContext db) : DbSectionProvider(db)
{
    public override string Key => ClinicalHistorySections.LabResults;

    public override Task<int> CountAsync(Guid catId, CancellationToken ct) =>
        Db.Set<DiagnosticOrderAggregate>().AsNoTracking().CountAsync(o => o.CatId == catId && !o.IsMarkedToDelete, ct);

    public override async Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct)
    {
        var orders = await Db.Set<DiagnosticOrderAggregate>().AsNoTracking()
            .Where(o => o.CatId == catId && !o.IsMarkedToDelete)
            .OrderByDescending(o => o.OrderedOn).ThenBy(o => o.Id)
            .Skip(skip).Take(take)
            .Select(o => new { o.Id, o.VisitId, o.Kind, o.Status, o.OrderedOn, ResultSetCount = o.ResultSets.Count, AttachmentCount = o.Attachments.Count })
            .ToListAsync(ct);

        var resultSets = await DiagnosticRows.ResultSetsAsync(Db, orders.Select(o => o.Id).ToList(), ct);

        return orders.Select(o =>
        {
            // The latest set: newest first, and a correction ahead of the set it supersedes when both carry the same instant.
            var latest = resultSets[o.Id].OrderByDescending(s => s.RecordedOn).ThenBy(s => s.SupersedesResultSetId != null ? 0 : 1).ThenBy(s => s.Id).FirstOrDefault();
            var flags = latest?.Values.GroupBy(v => v.Flag.DisplayName).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}:{g.Count()}") ?? [];
            var abnormal = latest?.Values.Any(v => v.Flag == ResultFlag.Low || v.Flag == ResultFlag.High || v.Flag == ResultFlag.Abnormal) ?? false;

            return new ClinicalHistoryItem(
                o.Id, "lab-result", o.OrderedOn, o.Kind.DisplayName, o.Status.DisplayName,
                Fields(
                    ("visitId", o.VisitId.ToString()),
                    ("reportedOn", latest?.ReportedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    ("flags", string.Join(",", flags)),
                    ("hasAbnormal", abnormal ? "true" : "false"),
                    ("resultSetCount", o.ResultSetCount.ToString(CultureInfo.InvariantCulture)),
                    ("attachmentCount", o.AttachmentCount.ToString(CultureInfo.InvariantCulture))));
        }).ToList();
    }
}

/// <summary><c>photos</c> -- clinical photographs with signed (short-lived) URLs (§8.7).</summary>
public sealed class PhotosSectionProvider(CatEcommerceDbContext db, IStoredFileSigningBatcher signer) : DbSectionProvider(db)
{
    public override string Key => ClinicalHistorySections.Photos;

    public override Task<int> CountAsync(Guid catId, CancellationToken ct) =>
        Db.Set<VisitAggregate>().AsNoTracking().Where(v => v.CatId == catId && !v.IsMarkedToDelete).SelectMany(v => v.Photos).CountAsync(ct);

    public override async Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct)
    {
        var photos = await Db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.CatId == catId && !v.IsMarkedToDelete)
            .SelectMany(v => v.Photos.Select(p => new { p.Id, p.FileId, p.TakenOn, p.Note, VisitId = v.Id }))
            .OrderByDescending(p => p.TakenOn).ThenBy(p => p.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        var files = await signer.SignManyAsync(photos.Select(p => p.FileId).ToList(), "inline", ct);

        return photos.Select(p => new ClinicalHistoryItem(
            p.Id, "photo", AtMidnightUtc(p.TakenOn), "Photograph", p.Note,
            Fields(("visitId", p.VisitId.ToString()), ("takenOn", p.TakenOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
            files.TryGetValue(p.FileId, out var file) ? file : null)).ToList();
    }
}

/// <summary><c>vaccinations</c> (data: HO-CAT-001) -- read-only over the cat's own table (§8.7).</summary>
public sealed class VaccinationsSectionProvider(CatEcommerceDbContext db) : DbSectionProvider(db)
{
    public override string Key => ClinicalHistorySections.Vaccinations;

    public override Task<int> CountAsync(Guid catId, CancellationToken ct) =>
        Db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == catId).SelectMany(c => c.Vaccinations).CountAsync(ct);

    public override async Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct)
    {
        var rows = await Db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == catId).SelectMany(c => c.Vaccinations)
            .OrderByDescending(v => v.AdministeredOn).ThenBy(v => v.Id)
            .Skip(skip).Take(take)
            .Select(v => new { v.Id, v.VaccineName, v.VaccineCode, v.AdministeredOn, v.NextDueOn, v.BatchReference })
            .ToListAsync(ct);

        return rows.Select(v => new ClinicalHistoryItem(
            v.Id, "vaccination", AtMidnightUtc(v.AdministeredOn), v.VaccineName, null,
            Fields(("code", v.VaccineCode), ("nextDueOn", v.NextDueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("batch", v.BatchReference)))).ToList();
    }
}

/// <summary><c>parasite-treatments</c> (data: HO-CAT-001).</summary>
public sealed class ParasiteTreatmentsSectionProvider(CatEcommerceDbContext db) : DbSectionProvider(db)
{
    public override string Key => ClinicalHistorySections.ParasiteTreatments;

    public override Task<int> CountAsync(Guid catId, CancellationToken ct) =>
        Db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == catId).SelectMany(c => c.ParasiteTreatments).CountAsync(ct);

    public override async Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct)
    {
        var rows = await Db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == catId).SelectMany(c => c.ParasiteTreatments)
            .OrderByDescending(t => t.AdministeredOn).ThenBy(t => t.Id)
            .Skip(skip).Take(take)
            .Select(t => new { t.Id, t.ProductName, t.TreatmentType, t.AdministeredOn, t.NextDueOn })
            .ToListAsync(ct);

        return rows.Select(t => new ClinicalHistoryItem(
            t.Id, "parasite-treatment", AtMidnightUtc(t.AdministeredOn), t.ProductName, t.TreatmentType.DisplayName,
            Fields(("nextDueOn", t.NextDueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))))).ToList();
    }
}

/// <summary><c>weights</c> (data: HO-CAT-001) -- served by <c>cat_weight_record (cat_id, recorded_on)</c>. A visit's vitals weight
/// is in this table too, under the entry's own id (ADR-121).</summary>
public sealed class WeightsSectionProvider(CatEcommerceDbContext db) : DbSectionProvider(db)
{
    public override string Key => ClinicalHistorySections.Weights;

    public override Task<int> CountAsync(Guid catId, CancellationToken ct) =>
        Db.CatWeightRecords.AsNoTracking().CountAsync(w => w.CatId == catId, ct);

    public override async Task<IReadOnlyList<ClinicalHistoryItem>> ReadAsync(Guid catId, int skip, int take, CancellationToken ct)
    {
        var rows = await Db.CatWeightRecords.AsNoTracking()
            .Where(w => w.CatId == catId)
            .OrderByDescending(w => w.RecordedOn).ThenBy(w => w.Id)
            .Skip(skip).Take(take)
            .Select(w => new { w.Id, w.RecordedOn, w.Weight.Kilograms })
            .ToListAsync(ct);

        return rows.Select(w => new ClinicalHistoryItem(
            w.Id, "weight", AtMidnightUtc(w.RecordedOn), $"{w.Kilograms.ToString("F2", CultureInfo.InvariantCulture)} kg", null,
            Fields(("kilograms", w.Kilograms.ToString("F2", CultureInfo.InvariantCulture)), ("unit", "kg")))).ToList();
    }
}

/// <summary>
/// <c>timeline</c> -- visits, photographs, lab results, vaccinations, parasite treatments and weights merged by date
/// (§8.7, US-CLN-13, US-CLN-19). Each source contributes its newest <c>pageNumber * pageSize</c> items (capped), the
/// merge takes the requested slice: nothing is read that a page cannot show, and no source is read in full.
/// </summary>
public sealed class TimelineSectionProvider(
    VisitsSectionProvider visits, PhotosSectionProvider photos, LabResultsSectionProvider labResults,
    VaccinationsSectionProvider vaccinations, ParasiteTreatmentsSectionProvider parasiteTreatments, WeightsSectionProvider weights)
    : IClinicalHistorySectionProvider
{
    /// <summary>The deepest a timeline can be paged -- a bound on what each source is asked for.</summary>
    public const int MaxDepth = 2_000;

    public string Key => ClinicalHistorySections.Timeline;

    private DbSectionProvider[] Sources => [visits, photos, labResults, vaccinations, parasiteTreatments, weights];

    private async Task<int> CountAsync(Guid catId, CancellationToken ct)
    {
        var total = 0;
        foreach (var source in Sources) total += await source.CountAsync(catId, ct);
        return total;
    }

    public async Task<ClinicalHistorySectionSummary> GetSummaryAsync(Guid catId, CancellationToken ct) =>
        new(Key, true, ClinicalHistorySections.Find(Key)!.Owner, await CountAsync(catId, ct));

    public async Task<PagedResult<ClinicalHistoryItem>> GetPageAsync(Guid catId, int pageNumber, int pageSize, CancellationToken ct)
    {
        var total = await CountAsync(catId, ct);
        var skip = (long)(pageNumber - 1) * pageSize;
        if (skip >= total || skip >= MaxDepth)
            return new PagedResult<ClinicalHistoryItem>([], pageNumber, pageSize, total);

        var depth = (int)Math.Min(skip + pageSize, MaxDepth);
        var merged = new List<ClinicalHistoryItem>();
        foreach (var source in Sources)
            merged.AddRange(await source.ReadAsync(catId, 0, depth, ct));

        var items = merged
            .OrderByDescending(i => i.OccurredOn).ThenBy(i => i.Kind, StringComparer.Ordinal).ThenBy(i => i.Id)
            .Skip((int)skip).Take(pageSize)
            .ToList();

        return new PagedResult<ClinicalHistoryItem>(items, pageNumber, pageSize, total);
    }
}
