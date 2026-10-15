using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Visit;

/// <summary>Staff display names for audit-style columns ("opened by", "recorded by"): one query for a set
/// of ids, never one lookup per row (.ai/read-side-pattern.md §4).</summary>
internal static class StaffNames
{
    public static async Task<IReadOnlyDictionary<Guid, string>> LookupAsync(CatEcommerceDbContext db, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var distinct = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();

        return await db.Set<StaffAccountAggregate>().AsNoTracking()
            .Where(s => distinct.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName })
            .ToDictionaryAsync(s => s.Id, s => s.FullName, ct);
    }

    public static string Of(IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } value && names.TryGetValue(value, out var name) ? name : "";
}

/// <summary>One active (not withdrawn) problem of a visit, as read.</summary>
internal sealed record ProblemRow(Guid VisitId, Guid Id, string Code, string Name, ProblemCertainty Certainty, DateTimeOffset AddedOn);

internal static class VisitProblems
{
    /// <summary>The active problems of the given visits in one query, oldest first per visit -- projected rows, not the visits.</summary>
    public static async Task<Dictionary<Guid, List<ProblemRow>>> ActiveAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> visitIds, CancellationToken ct = default)
    {
        var result = visitIds.ToDictionary(id => id, _ => new List<ProblemRow>());
        if (visitIds.Count == 0) return result;

        var rows = await db.Set<VisitAggregate>().AsNoTracking().Where(v => visitIds.Contains(v.Id))
            .SelectMany(v => v.Problems.Where(p => p.WithdrawnOn == null).Select(p => new ProblemRow(v.Id, p.Id, p.Code, p.Name, p.Certainty, p.AddedOn)))
            .ToListAsync(ct);

        foreach (var row in rows.OrderBy(r => r.AddedOn).ThenBy(r => r.Id)) result[row.VisitId].Add(row);
        return result;
    }
}

/// <summary>One vitals entry as read: the columns only, projected from the owned row (not the entity).</summary>
internal sealed record VitalsRow(
    Guid Id, DateTimeOffset MeasuredOn, decimal? WeightKg, decimal? TemperatureC, int? HeartRateBpm, int? RespiratoryRatePerMin,
    int? BodyConditionScore, int? PainScore, int[] ConfirmedOutOfRange, string? Note, Guid RecordedBy, DateTimeOffset RecordedOn)
{
    public static readonly System.Linq.Expressions.Expression<Func<VitalsEntry, VitalsRow>> From = e => new VitalsRow(
        e.Id, e.MeasuredOn, e.WeightKg, e.TemperatureC, e.HeartRateBpm, e.RespiratoryRatePerMin, e.BodyConditionScore, e.PainScore,
        e.ConfirmedOutOfRange, e.Note, e.RecordedBy, e.RecordedOn);
}

/// <summary>§12.4 <c>GET /api/staff/visits/{visitId}</c> -- the whole visit in one payload (§8.4). A visit of
/// another branch is "not found" (backlog #8).</summary>
public sealed class GetVisitDetailQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer) : IQueryHandlerAsync<GetVisitDetailQuery, VisitDetailViewModel?>
{
    /// <summary>The visit's own columns (the note and the follow-up are columns of <c>clinic.visit</c>), projected --
    /// no aggregate is rehydrated (.ai/read-side-pattern.md rules 1-2).</summary>
    private sealed record VisitHeader(
        Guid Id, int Version, VisitStatus Status, Guid CatId, Guid AppointmentId, Guid ClinicServiceId, string ServiceName,
        ServiceCategory ServiceCategory, DateTimeOffset OpenedOn, Guid OpenedBy, DateTimeOffset? FinalisedOn, Guid? FinalisedBy,
        string Subjective, string Objective, string Assessment, string Plan, string DischargeInstructions,
        DateTimeOffset? NoteSavedOn, Guid? NoteSavedBy, Guid? AppliedTemplateId, int? AppliedTemplateVersion,
        FollowUpKind? FollowUpKind, DateOnly? FollowUpDueOn, Guid? FollowUpAppointmentId, DateTimeOffset? FollowUpSetOn);

    public async Task<QueryResponse<VisitDetailViewModel?>> HandleAsync(GetVisitDetailQuery query)
    {
        var visit = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.Id == query.VisitId && v.BranchId == query.BranchId && !v.IsMarkedToDelete)
            .Select(v => new VisitHeader(
                v.Id, v.Version, v.Status, v.CatId, v.AppointmentId, v.ClinicServiceId, v.ServiceName, v.ServiceCategory, v.OpenedOn, v.OpenedBy,
                v.FinalisedOn, v.FinalisedBy, v.Note.Subjective, v.Note.Objective, v.Note.Assessment, v.Note.Plan, v.Note.DischargeInstructions,
                v.Note.NoteSavedOn, v.Note.NoteSavedBy, v.Note.AppliedTemplateId, v.Note.AppliedTemplateVersion,
                v.FollowUp == null ? null : v.FollowUp.Kind, v.FollowUp == null ? null : (DateOnly?)v.FollowUp.DueOn,
                v.FollowUp == null ? null : v.FollowUp.AppointmentId, v.FollowUp == null ? null : (DateTimeOffset?)v.FollowUp.SetOn))
            .FirstOrDefaultAsync();
        if (visit is null) return new QueryResponse<VisitDetailViewModel?> { Result = null };

        var owned = db.Set<VisitAggregate>().AsNoTracking().Where(v => v.Id == visit.Id);

        var cat = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == visit.CatId).Select(c => new { c.Name }).FirstOrDefaultAsync();
        var appointment = await db.Set<AppointmentAggregate>().AsNoTracking().Where(a => a.Id == visit.AppointmentId)
            .Select(a => new { a.Id, a.Status, a.StartsOn, a.PerformerId }).FirstOrDefaultAsync();
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";

        var problems = await owned.SelectMany(v => v.Problems).Where(p => p.WithdrawnOn == null).OrderBy(p => p.AddedOn).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.DiagnosisCodeId, p.Code, p.Name, p.Certainty, p.Note }).ToListAsync();
        var vitals = await owned.SelectMany(v => v.Vitals).OrderBy(e => e.MeasuredOn).ThenBy(e => e.RecordedOn).ThenBy(e => e.Id)
            .Select(VitalsRow.From).ToListAsync();
        var photos = await owned.SelectMany(v => v.Photos).OrderBy(p => p.TakenOn).ThenBy(p => p.AddedOn).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.FileId, p.TakenOn, p.Note, p.AddedOn }).ToListAsync();

        var treatments = await TreatmentRows.OfVisitAsync(db, visit.Id);

        // Addenda are separate roots, returned in AddedOn order beneath the original text (§8.4).
        var addenda = await db.Set<VisitAddendumAggregate>().AsNoTracking()
            .Where(a => a.VisitId == visit.Id && !a.IsMarkedToDelete)
            .OrderBy(a => a.AddedOn).ThenBy(a => a.Id)
            .Select(a => new { a.Id, a.Text, a.Reason, a.AuthorStaffId, a.AddedOn })
            .ToListAsync();

        var staffIds = new List<Guid?> { visit.OpenedBy, visit.FinalisedBy, visit.NoteSavedBy, appointment?.PerformerId };
        staffIds.AddRange(vitals.Select(v => (Guid?)v.RecordedBy));
        staffIds.AddRange(addenda.Select(a => (Guid?)a.AuthorStaffId));
        staffIds.AddRange(treatments.SelectMany(t => new Guid?[] { t.RecordedBy, t.ReversedBy }));
        var names = await StaffNames.LookupAsync(db, staffIds);

        // Photographs as short-lived signed URLs; there is no server-side thumbnail in v1, so the thumbnail is the same signed file.
        var photoFiles = await signer.SignManyAsync(photos.Select(p => p.FileId).ToList(), "inline");

        // Two figures per order, computed in the database -- never the orders' result sets and values.
        var diagnostics = await db.Set<DiagnosticOrderAggregate>().AsNoTracking()
            .Where(o => o.VisitId == visit.Id && !o.IsMarkedToDelete)
            .OrderBy(o => o.OrderedOn).ThenBy(o => o.Id)
            .Select(o => new
            {
                o.Id, o.Kind, o.Status, o.OrderedOn,
                ResultSetCount = o.ResultSets.Count,
                HasAbnormal = o.ResultSets.Any(r => r.Values.Any(v => v.Flag == ResultFlag.Low || v.Flag == ResultFlag.High || v.Flag == ResultFlag.Abnormal))
            })
            .ToListAsync();

        var isOpen = visit.Status == VisitStatus.Open;
        var template = isOpen && visit.NoteSavedOn is null ? await ReadTemplateAsync(visit.ClinicServiceId) : null;
        var draft = isOpen ? await ReadDraftAsync(visit.Id, query.UserContext.UserId, visit.NoteSavedOn) : null;

        var appointmentStatus = appointment?.Status ?? AppointmentStatus.CheckedIn;
        var detail = new VisitDetailViewModel(
            visit.Id, visit.Version, visit.Status.DisplayName, visit.CatId, cat?.Name ?? "",
            new VisitAppointmentViewModel(visit.AppointmentId, appointmentStatus.DisplayName, appointment?.StartsOn, appointment?.PerformerId is null ? null : StaffNames.Of(names, appointment.PerformerId)),
            new VisitServiceViewModel(visit.ClinicServiceId, visit.ServiceName, visit.ServiceCategory.DisplayName),
            visit.OpenedOn, StaffNames.Of(names, visit.OpenedBy), timeZoneId,
            VisitAllowedActions.For(visit.Status, query.CallerRoles, appointmentStatus),
            new VisitNoteViewModel(
                visit.Subjective, visit.Objective, visit.Assessment, visit.Plan, visit.DischargeInstructions,
                visit.NoteSavedOn, visit.NoteSavedBy is null ? null : StaffNames.Of(names, visit.NoteSavedBy),
                visit.AppliedTemplateId, visit.AppliedTemplateVersion),
            problems.Select(p => new VisitProblemViewModel(p.Id, p.DiagnosisCodeId, p.Code, p.Name, p.Certainty.DisplayName, p.Note)).ToList(),
            vitals.Select(v => ToViewModel(v, names)).ToList(),
            treatments.Select(t => TreatmentRows.ToViewModel(t, names)).ToList(),
            photos.Select(p => photoFiles.TryGetValue(p.FileId, out var file)
                ? new VisitPhotoViewModel(p.Id, p.TakenOn, p.Note, p.AddedOn, file.Url, file.Url, file.ContentType, file.UrlExpiresOn)
                : new VisitPhotoViewModel(p.Id, p.TakenOn, p.Note, p.AddedOn, null, null, null, null)).ToList(),
            visit.FollowUpKind is null ? null : new VisitFollowUpViewModel(visit.FollowUpKind.DisplayName, visit.FollowUpDueOn!.Value, visit.FollowUpAppointmentId, visit.FollowUpSetOn!.Value),
            addenda.Select(a => new VisitAddendumViewModel(a.Id, a.Text, a.Reason, StaffNames.Of(names, a.AuthorStaffId), a.AddedOn)).ToList(),
            diagnostics.Select(o => new VisitDiagnosticSummaryViewModel(o.Id, o.Kind.DisplayName, o.Status.DisplayName, o.OrderedOn, o.ResultSetCount, o.HasAbnormal)).ToList(),
            template,
            draft,
            visit.FinalisedOn, visit.FinalisedBy is null ? null : StaffNames.Of(names, visit.FinalisedBy));

        return new QueryResponse<VisitDetailViewModel?> { Result = detail };
    }

    /// <summary>US-CLN-20 -- the service's one active template, offered only while no note has ever been saved.
    /// The client pre-fills; the note stays fully editable (ADR-134).</summary>
    private async Task<VisitTemplateViewModel?> ReadTemplateAsync(Guid clinicServiceId)
    {
        var t = await db.Set<NoteTemplateAggregate>().AsNoTracking()
            .Where(x => x.ClinicServiceId == clinicServiceId && x.IsActive && !x.IsMarkedToDelete)
            .Select(x => new { x.Id, x.TemplateVersion, x.Name, x.Subjective, x.Objective, x.Assessment, x.Plan, x.DischargeInstructions })
            .FirstOrDefaultAsync();

        return t is null ? null : new VisitTemplateViewModel(t.Id, t.TemplateVersion, t.Name, t.Subjective, t.Objective, t.Assessment, t.Plan, t.DischargeInstructions);
    }

    /// <summary>US-CLN-12 AC2 / ADR-122 -- the <b>caller's own</b> working copy, and only when it is newer than the
    /// saved note (or no note has been saved). Another author's draft is never returned.</summary>
    private async Task<VisitDraftViewModel?> ReadDraftAsync(Guid visitId, Guid authorId, DateTimeOffset? noteSavedOn)
    {
        var d = await db.Set<ConsultationDraftAggregate>().AsNoTracking()
            .Where(x => x.VisitId == visitId && x.AuthorStaffId == authorId && !x.IsMarkedToDelete)
            .Select(x => new { x.Subjective, x.Objective, x.Assessment, x.Plan, x.DischargeInstructions, x.ProblemCodeIds, x.SavedOn })
            .FirstOrDefaultAsync();

        if (d is null || (noteSavedOn is { } saved && d.SavedOn <= saved)) return null;

        return new VisitDraftViewModel(d.Subjective, d.Objective, d.Assessment, d.Plan, d.DischargeInstructions, d.ProblemCodeIds.ToList(), d.SavedOn);
    }

    internal static VitalsEntryViewModel ToViewModel(VitalsRow v, IReadOnlyDictionary<Guid, string> names) => new(
        v.Id, v.MeasuredOn,
        Of(VitalSign.Weight, v.WeightKg), Of(VitalSign.Temperature, v.TemperatureC), Of(VitalSign.HeartRate, v.HeartRateBpm),
        Of(VitalSign.RespiratoryRate, v.RespiratoryRatePerMin), Of(VitalSign.BodyConditionScore, v.BodyConditionScore), Of(VitalSign.PainScore, v.PainScore),
        v.ConfirmedOutOfRange.Select(value => Enumeration.FromValue<VitalSign>(value).DisplayName).ToList(), v.Note, StaffNames.Of(names, v.RecordedBy), v.RecordedOn);

    private static Measurement? Of(VitalSign sign, decimal? value)
    {
        if (value is null) return null;
        var range = VitalSignRules.For(sign);
        return Measurement.Of(value.Value, range.DecimalPlaces, range.Unit);
    }
}

/// <summary>§12.4 <c>GET /api/staff/visits?status&amp;date</c> -- the active branch's visits.</summary>
public sealed class GetOpenVisitsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetOpenVisitsQuery, IReadOnlyList<VisitListItemViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<VisitListItemViewModel>>> HandleAsync(GetOpenVisitsQuery query)
    {
        var status = EnumerationParsing.TryParse<VisitStatus>(query.Status, out var parsed) && parsed is not null ? parsed : VisitStatus.Open;

        var visits = db.Set<VisitAggregate>().AsNoTracking().Where(v => v.BranchId == query.BranchId && v.Status == status && !v.IsMarkedToDelete);

        if (query.Date is { } date)
        {
            var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
            var from = AppointmentSlotFinder.InstantOf(date, TimeOnly.MinValue, timeZoneId);
            var to = AppointmentSlotFinder.InstantOf(date.AddDays(1), TimeOnly.MinValue, timeZoneId);
            visits = visits.Where(v => v.OpenedOn >= from && v.OpenedOn < to);
        }

        var rows = await (
            from v in visits
            join c in db.Set<CatAggregate>().AsNoTracking() on v.CatId equals c.Id into cats
            from c in cats.DefaultIfEmpty()
            orderby v.OpenedOn
            select new
            {
                v.Id, v.AppointmentId, v.CatId, CatName = c != null ? c.Name : "", CatIsHighRisk = c != null && c.IsHighRisk,
                v.ServiceName, v.OpenedOn, v.Status, HasVitals = v.Vitals.Any(),
                HasNote = v.Note.NoteSavedOn != null
            }).Take(500).ToListAsync();

        IReadOnlyList<VisitListItemViewModel> items = rows
            .Select(r => new VisitListItemViewModel(r.Id, r.AppointmentId, new VisitListCatViewModel(r.CatId, r.CatName, r.CatIsHighRisk), r.ServiceName, r.OpenedOn, r.Status.DisplayName, r.HasVitals, r.HasNote))
            .ToList();

        return new QueryResponse<IReadOnlyList<VisitListItemViewModel>> { Result = items };
    }
}

public sealed class GetVisitByAppointmentQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetVisitByAppointmentQuery, VisitReferenceViewModel>
{
    public async Task<QueryResponse<VisitReferenceViewModel>> HandleAsync(GetVisitByAppointmentQuery query)
    {
        var row = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.AppointmentId == query.AppointmentId && v.BranchId == query.BranchId && !v.IsMarkedToDelete)
            .Select(v => new { v.Id, v.Status })
            .FirstOrDefaultAsync();

        return new QueryResponse<VisitReferenceViewModel>
        {
            Result = row is null ? new VisitReferenceViewModel(null, null) : new VisitReferenceViewModel(row.Id, row.Status.DisplayName)
        };
    }
}

/// <summary>§12.4 <c>GET /api/staff/clinical/vital-sign-rules</c> -- the same table the aggregate decides by.</summary>
public sealed class GetVitalSignRulesQueryHandler : IQueryHandlerAsync<GetVitalSignRulesQuery, IReadOnlyList<VitalSignRuleViewModel>>
{
    public Task<QueryResponse<IReadOnlyList<VitalSignRuleViewModel>>> HandleAsync(GetVitalSignRulesQuery query)
    {
        IReadOnlyList<VitalSignRuleViewModel> rules = VitalSignRules.All
            .Select(r => new VitalSignRuleViewModel(
                r.Sign.DisplayName, r.Unit,
                VitalSignRules.Format(r.Sign, r.AbsoluteMin), VitalSignRules.Format(r.Sign, r.AbsoluteMax),
                r.PlausibleMin is { } min ? VitalSignRules.Format(r.Sign, min) : null,
                r.PlausibleMax is { } max ? VitalSignRules.Format(r.Sign, max) : null,
                r.DecimalPlaces))
            .ToList();

        return Task.FromResult(new QueryResponse<IReadOnlyList<VitalSignRuleViewModel>> { Result = rules });
    }
}
