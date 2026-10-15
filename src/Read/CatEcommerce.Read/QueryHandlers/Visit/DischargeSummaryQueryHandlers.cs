using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ClinicalHistory;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Visit;

/// <summary>
/// Builds the derived discharge summary (§8.8, ADR-130) from a visit the caller has <b>already been allowed to see</b>: staff
/// by branch, a customer by current ownership. A visit that is not finalised yields <c>null</c> -- a draft is not a summary.
/// Nothing is stored; addenda are always read fresh, so a correction appears under "Corrections" the moment it is made.
/// </summary>
internal static class DischargeSummaryBuilder
{
    /// <summary>The visit columns a summary is built from, projected (the visit is never rehydrated).</summary>
    internal sealed record VisitHeader(
        Guid Id, Guid BranchId, Guid CatId, VisitStatus Status, DateTimeOffset OpenedOn, Guid OpenedBy, Guid? FinalisedBy, string DischargeInstructions,
        FollowUpKind? FollowUpKind, DateOnly? FollowUpDueOn, Guid? FollowUpAppointmentId);

    public static readonly System.Linq.Expressions.Expression<Func<VisitAggregate, VisitHeader>> Header = v => new VisitHeader(
        v.Id, v.BranchId, v.CatId, v.Status, v.OpenedOn, v.OpenedBy, v.FinalisedBy, v.Note.DischargeInstructions,
        v.FollowUp == null ? null : v.FollowUp.Kind, v.FollowUp == null ? null : (DateOnly?)v.FollowUp.DueOn, v.FollowUp == null ? null : v.FollowUp.AppointmentId);

    public static async Task<DischargeSummaryViewModel?> BuildAsync(
        CatEcommerceDbContext db, IEnumerable<IDischargeSummaryContributor> contributors, VisitHeader visit, CancellationToken ct)
    {
        if (visit.Status != VisitStatus.Finalised) return null;

        var branch = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == visit.BranchId)
            .Select(b => new { b.Name, b.AddressLine, b.PhoneNumber, b.TimeZoneId }).FirstOrDefaultAsync(ct);
        var cat = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == visit.CatId)
            .Select(c => new { c.Name, c.Sex, c.DateOfBirth }).FirstOrDefaultAsync(ct);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        var visitDate = BranchCalendar.LocalDateOf(visit.OpenedOn, timeZoneId);

        var addenda = await db.Set<VisitAddendumAggregate>().AsNoTracking()
            .Where(a => a.VisitId == visit.Id && !a.IsMarkedToDelete).OrderBy(a => a.AddedOn)
            .Select(a => new { a.Text, a.Reason, a.AddedOn }).ToListAsync(ct);

        DateTimeOffset? followUpStartsOn = null;
        if (visit.FollowUpAppointmentId is { } appointmentId)
            followUpStartsOn = await db.Set<AppointmentAggregate>().AsNoTracking().Where(a => a.Id == appointmentId).Select(a => a.StartsOn).FirstOrDefaultAsync(ct);

        var names = await StaffNames.LookupAsync(db, [visit.FinalisedBy ?? visit.OpenedBy], ct);

        var extraSections = new List<DischargeSummarySectionViewModel>();
        var contributorList = contributors.ToList();
        foreach (var contributor in contributorList)
        {
            if (await contributor.ContributeAsync(visit.Id, visit.CatId, ct) is { } section) extraSections.Add(section);
        }

        var weight = await db.Set<VisitAggregate>().AsNoTracking().Where(v => v.Id == visit.Id).SelectMany(v => v.Vitals)
            .Where(e => e.WeightKg != null).OrderByDescending(e => e.MeasuredOn).ThenByDescending(e => e.RecordedOn).Select(e => e.WeightKg).FirstOrDefaultAsync(ct);
        var problems = (await VisitProblems.ActiveAsync(db, [visit.Id], ct))[visit.Id];

        // Section 8.8: treatments are the non-reversed lines; medicines administered are the Medicine lines among them.
        var treatments = (await TreatmentRows.OfVisitAsync(db, visit.Id, ct)).Where(t => !t.IsReversed).ToList();
        var weightRange = VitalSignRules.For(VitalSign.Weight);

        return new DischargeSummaryViewModel(
            visit.Id, branch?.Name ?? "", branch?.AddressLine ?? "", branch?.PhoneNumber, timeZoneId,
            visit.CatId, cat?.Name ?? "", cat?.Sex.DisplayName ?? "", CatAge(cat?.DateOfBirth, visitDate),
            weight is { } kg ? Measurement.Of(kg, weightRange.DecimalPlaces, weightRange.Unit) : null,
            visitDate, StaffNames.Of(names, visit.FinalisedBy ?? visit.OpenedBy),
            problems.Select(p => new DischargeSummaryProblemViewModel(p.Name, p.Code, p.Certainty.DisplayName)).ToList(),
            treatments.Select(TreatmentRows.SummaryLine).ToList(),
            treatments.Where(t => t.Kind == TreatmentKind.Medicine).Select(TreatmentRows.SummaryLine).ToList(),
            visit.DischargeInstructions,
            visit.FollowUpKind is null ? null : new DischargeSummaryFollowUpViewModel(visit.FollowUpKind.DisplayName, visit.FollowUpDueOn!.Value, followUpStartsOn),
            addenda.Select(a => new DischargeSummaryCorrectionViewModel(a.AddedOn, a.Text, a.Reason)).ToList(),
            extraSections,
            contributorList.Count > 0);
    }

    /// <summary>"2 years 3 months" at the visit date; "Age unknown" without a date of birth.</summary>
    public static string CatAge(DateOnly? dateOfBirth, DateOnly on)
    {
        if (dateOfBirth is not { } dob || dob > on) return "age unknown";

        var months = (on.Year - dob.Year) * 12 + on.Month - dob.Month - (on.Day < dob.Day ? 1 : 0);
        var years = months / 12;
        var rest = months % 12;

        if (years == 0) return rest == 1 ? "1 month" : $"{rest} months";
        var yearText = years == 1 ? "1 year" : $"{years} years";
        return rest == 0 ? yearText : $"{yearText} {(rest == 1 ? "1 month" : $"{rest} months")}";
    }

    /// <summary>The treatments and medicines given (section 8.8) come first, then whatever contributors add (HO-PHM-001: take-home prescriptions).</summary>
    private static IReadOnlyList<CatEcommerce.Domain.Models.DischargeSummarySection> PrintedSections(DischargeSummaryViewModel s)
    {
        var sections = new List<CatEcommerce.Domain.Models.DischargeSummarySection>();
        if (s.Treatments.Count > 0) sections.Add(new CatEcommerce.Domain.Models.DischargeSummarySection("Treatments", s.Treatments));
        if (s.MedicinesAdministered.Count > 0) sections.Add(new CatEcommerce.Domain.Models.DischargeSummarySection("Medicines given", s.MedicinesAdministered));
        sections.AddRange(s.ExtraSections.Select(e => new CatEcommerce.Domain.Models.DischargeSummarySection(e.Title, e.Lines)));
        return sections;
    }

    /// <summary>The view model as the plain-data document the PDF renderer prints (local times, plain text).</summary>
    public static DischargeSummaryDocument ToDocument(DischargeSummaryViewModel s)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(s.BranchTimeZoneId);
        string Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone).ToString("d MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        string? followUp = s.FollowUp is null ? null
            : s.FollowUp.AppointmentStartsOn is { } startsOn
                ? $"Booked for {Local(startsOn)}"
                : $"Please book a visit around {s.FollowUp.DueOn.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}";

        return new DischargeSummaryDocument(
            s.BranchName, s.BranchAddress, s.BranchPhone, s.CatName, s.CatSex, s.CatAge,
            s.WeightAtVisit is null ? null : $"{s.WeightAtVisit.Value} {s.WeightAtVisit.Unit}",
            s.VisitDate.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), s.AttendingVet,
            s.Problems.Select(p => new DischargeSummaryProblem(p.Name, p.Certainty)).ToList(),
            s.HomeCareInstructions, followUp,
            s.Corrections.Select(c => new DischargeSummaryCorrection(Local(c.AddedOn), c.Text, c.Reason)).ToList(),
            PrintedSections(s),
            s.PrescriptionsAvailable);
    }
}

/// <summary>Public face of the summary-to-document mapping, so a controller never reaches into the builder.</summary>
public static class DischargeSummaryDocuments
{
    public static DischargeSummaryDocument From(DischargeSummaryViewModel summary) => DischargeSummaryBuilder.ToDocument(summary);
}

public sealed class GetDischargeSummaryQueryHandler(CatEcommerceDbContext db, IEnumerable<IDischargeSummaryContributor> contributors)
    : IQueryHandlerAsync<GetDischargeSummaryQuery, DischargeSummaryViewModel?>
{
    public async Task<QueryResponse<DischargeSummaryViewModel?>> HandleAsync(GetDischargeSummaryQuery query)
    {
        var visit = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.Id == query.VisitId && v.BranchId == query.BranchId && !v.IsMarkedToDelete)
            .Select(DischargeSummaryBuilder.Header).FirstOrDefaultAsync();

        return new QueryResponse<DischargeSummaryViewModel?> { Result = visit is null ? null : await DischargeSummaryBuilder.BuildAsync(db, contributors, visit, default) };
    }
}

/// <summary>The owner reads their cat's summary: the visit's cat must belong to them <b>now</b> and the visit must be finalised.</summary>
public sealed class GetCustomerVisitSummaryQueryHandler(CatEcommerceDbContext db, IEnumerable<IDischargeSummaryContributor> contributors)
    : IQueryHandlerAsync<GetCustomerVisitSummaryQuery, DischargeSummaryViewModel?>
{
    public async Task<QueryResponse<DischargeSummaryViewModel?>> HandleAsync(GetCustomerVisitSummaryQuery query)
    {
        var visit = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.Id == query.VisitId && v.Status == VisitStatus.Finalised && !v.IsMarkedToDelete)
            .Select(DischargeSummaryBuilder.Header).FirstOrDefaultAsync();
        if (visit is null) return new QueryResponse<DischargeSummaryViewModel?> { Result = null };

        var owned = await db.Set<CatAggregate>().AsNoTracking().AnyAsync(c => c.Id == visit.CatId && c.OwnerId == query.CustomerId && !c.IsMarkedToDelete);

        return new QueryResponse<DischargeSummaryViewModel?> { Result = owned ? await DischargeSummaryBuilder.BuildAsync(db, contributors, visit, default) : null };
    }
}

/// <summary>The owner's list of their cat's finalised visits. A cat that is not (now) theirs is <c>null</c>, i.e. 404.</summary>
public sealed class GetCustomerCatVisitsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCustomerCatVisitsQuery, IReadOnlyList<CustomerVisitListItemViewModel>?>
{
    public async Task<QueryResponse<IReadOnlyList<CustomerVisitListItemViewModel>?>> HandleAsync(GetCustomerCatVisitsQuery query)
    {
        var owned = await db.Set<CatAggregate>().AsNoTracking().AnyAsync(c => c.Id == query.CatId && c.OwnerId == query.CustomerId && !c.IsMarkedToDelete);
        if (!owned) return new QueryResponse<IReadOnlyList<CustomerVisitListItemViewModel>?> { Result = null };

        var visits = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.CatId == query.CatId && v.Status == VisitStatus.Finalised && !v.IsMarkedToDelete)
            .OrderByDescending(v => v.OpenedOn).ThenBy(v => v.Id)
            .Take(200)
            .Select(v => new { v.Id, v.BranchId, v.OpenedOn, v.ServiceName, v.OpenedBy, v.FinalisedBy, FollowUpOn = v.FollowUp == null ? (DateOnly?)null : v.FollowUp.DueOn })
            .ToListAsync();

        var branchIds = visits.Select(v => v.BranchId).Distinct().ToList();
        var zones = await db.Set<BranchAggregate>().AsNoTracking().Where(b => branchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.TimeZoneId }).ToDictionaryAsync(b => b.Id, b => b.TimeZoneId);
        var problems = await VisitProblems.ActiveAsync(db, visits.Select(v => v.Id).ToList());
        var names = await StaffNames.LookupAsync(db, visits.Select(v => v.FinalisedBy ?? v.OpenedBy).Select(id => (Guid?)id));

        IReadOnlyList<CustomerVisitListItemViewModel> items = visits.Select(v => new CustomerVisitListItemViewModel(
            v.Id, BranchCalendar.LocalDateOf(v.OpenedOn, zones.TryGetValue(v.BranchId, out var zone) ? zone : "UTC"), v.ServiceName,
            StaffNames.Of(names, v.FinalisedBy ?? v.OpenedBy), problems[v.Id].Select(p => p.Name).ToList(), true, v.FollowUpOn)).ToList();

        return new QueryResponse<IReadOnlyList<CustomerVisitListItemViewModel>?> { Result = items };
    }
}

/// <summary>§12.4 -- the owner's lab results: result sets of orders on <b>finalised</b> visits only, with flags and the ranges they
/// were judged against; attachments as signed URLs, <b>except DICOM</b> (a radiology file is not for the portal).</summary>
public sealed class GetCustomerLabResultsQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer)
    : IQueryHandlerAsync<GetCustomerLabResultsQuery, IReadOnlyList<CustomerLabResultViewModel>?>
{
    public async Task<QueryResponse<IReadOnlyList<CustomerLabResultViewModel>?>> HandleAsync(GetCustomerLabResultsQuery query)
    {
        var owned = await db.Set<CatAggregate>().AsNoTracking().AnyAsync(c => c.Id == query.CatId && c.OwnerId == query.CustomerId && !c.IsMarkedToDelete);
        if (!owned) return new QueryResponse<IReadOnlyList<CustomerLabResultViewModel>?> { Result = null };

        var finalisedVisits = db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.CatId == query.CatId && v.Status == VisitStatus.Finalised && !v.IsMarkedToDelete).Select(v => v.Id);

        var orders = await db.Set<DiagnosticOrderAggregate>().AsNoTracking()
            .Where(o => o.CatId == query.CatId && !o.IsMarkedToDelete && o.Status != DiagnosticOrderStatus.Cancelled && finalisedVisits.Contains(o.VisitId))
            .OrderByDescending(o => o.OrderedOn).ThenBy(o => o.Id)
            .Take(200)
            .Select(o => new { o.Id, o.VisitId, o.Kind, o.OrderedOn })
            .ToListAsync();

        var orderIds = orders.Select(o => o.Id).ToList();
        var resultSets = await DiagnosticRows.ResultSetsAsync(db, orderIds);
        var attachments = await DiagnosticRows.AttachmentsAsync(db, orderIds);

        // The portal shows what was found, not which staff member recorded it: no staff names go to a customer.
        IReadOnlyDictionary<Guid, string> noStaffNames = new Dictionary<Guid, string>();

        var fileIds = attachments.Values.SelectMany(list => list.Select(a => a.FileId)).Distinct().ToList();
        var contentTypes = fileIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StoredFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.ContentType);
        var signed = await signer.SignManyAsync(fileIds.Where(id => contentTypes.TryGetValue(id, out var type) && type != "application/dicom").ToList(), "inline");

        IReadOnlyList<CustomerLabResultViewModel> items = orders
            .Where(o => resultSets[o.Id].Count > 0 || attachments[o.Id].Count > 0)
            .Select(o => new CustomerLabResultViewModel(
                o.Id, o.VisitId, o.Kind.DisplayName, o.OrderedOn,
                resultSets[o.Id].Select(s => DiagnosticRows.ToViewModel(s, noStaffNames)).ToList(),
                attachments[o.Id].Where(a => signed.ContainsKey(a.FileId))
                    .Select(a => new DiagnosticAttachmentViewModel(a.Id, a.ResultDate, a.Note, "", a.AttachedOn, signed[a.FileId])).ToList()))
            .ToList();

        return new QueryResponse<IReadOnlyList<CustomerLabResultViewModel>?> { Result = items };
    }
}
