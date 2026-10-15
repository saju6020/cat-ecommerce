using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>R1 -- <c>clinic-daily-schedule</c> (US-RPT-01 AC1).</summary>
public sealed class ClinicDailyScheduleSource : IReportSource
{
    public string Key => ReportCatalogue.ClinicDailyScheduleKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var rows = await ClinicQueries.AppointmentsInRange(db, request).OrderBy(a => a.StartsOn ?? a.CheckedInOn).Take(request.RowLimit).ToListAsync(ct);
        var (output, totals) = ClinicScheduleBuilder.Build(rows);
        return new ReportSourceResult(output, totals);
    }
}

/// <summary>R2 -- <c>clinic-queue-performance</c> (US-RPT-01 AC1, "queue state").</summary>
public sealed class ClinicQueuePerformanceSource : IReportSource
{
    public string Key => ReportCatalogue.ClinicQueuePerformanceKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var appointments = await ClinicQueries.AppointmentsInRange(db, request).ToListAsync(ct);
        var (rows, totals) = QueuePerformanceBuilder.Build(appointments, request.From, request.To, request.LocalDate(request.Now), request.LocalDate);
        return new ReportSourceResult(rows, totals);
    }
}

/// <summary>R3 -- <c>surgery-register</c> (US-RPT-01 AC1).</summary>
public sealed class SurgeryRegisterSource : IReportSource
{
    public string Key => ReportCatalogue.SurgeryRegisterKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var query = db.Set<RvSurgeryRow>().AsNoTracking().Where(s => s.OccurredOn >= request.StartInclusive && s.OccurredOn < request.EndExclusive);
        if (!request.AllBranches) query = query.Where(s => s.BranchId == request.BranchId);

        var rows = await query.OrderBy(s => s.OccurredOn).Take(request.RowLimit).ToListAsync(ct);
        var (output, totals) = SurgeryRegisterBuilder.Build(rows);
        return new ReportSourceResult(output, totals);
    }
}

/// <summary>R4 -- <c>vaccination-due-list</c> (US-RPT-01 AC1). The status of each latest record is HO-CAT-001's own calculator; the branch of a cat is that of its latest appointment (ADR-292).</summary>
public sealed class VaccinationDueListSource : IReportSource
{
    public string Key => ReportCatalogue.VaccinationDueListKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var today = request.LocalDate(request.Now);
        var includeParasite = request.Flag("includeParasiteControl");
        var alive = CatLifeStatus.Alive.Value;
        var active = CustomerAccountStatus.Active.Value;
        var to = request.To;

        // Candidate cats: alive, an active owner, and at least one record due on or before the end of the window (the latest record decides later, in the builder).
        var candidates = await db.Set<RvCatVaccinationRow>().AsNoTracking()
            .Where(v => v.NextDueOn <= to && v.CatLifeStatus == alive && v.OwnerStatus == active && v.VaccineActive)
            .Select(v => v.CatId).Distinct().ToListAsync(ct);

        if (includeParasite)
        {
            var dewormers = new[] { ParasiteTreatmentType.Deworming.Value, ParasiteTreatmentType.Combined.Value };
            candidates = candidates.Union(await db.Set<RvCatParasiteTreatmentRow>().AsNoTracking()
                .Where(t => t.NextDueOn <= to && t.CatLifeStatus == alive && t.OwnerStatus == active && dewormers.Contains(t.TreatmentType))
                .Select(t => t.CatId).Distinct().ToListAsync(ct)).ToList();
        }

        var lastBranch = await db.Set<RvCatLastBranchRow>().AsNoTracking().Where(b => candidates.Contains(b.CatId)).ToListAsync(ct);
        var lastBranchByCat = lastBranch.ToDictionary(b => b.CatId, b => b.BranchId);

        // ADR-292: narrow to the branch (or to the unattributed cats, when asked) before loading every record of the survivors.
        var cats = request.AllBranches
            ? candidates
            : candidates.Where(c => lastBranchByCat.TryGetValue(c, out var branch) ? branch == request.BranchId : request.Flag("includeUnattributed")).ToList();

        var vaccinations = await db.Set<RvCatVaccinationRow>().AsNoTracking().Where(v => cats.Contains(v.CatId) && v.VaccineActive).ToListAsync(ct);
        var treatments = includeParasite
            ? await db.Set<RvCatParasiteTreatmentRow>().AsNoTracking().Where(t => cats.Contains(t.CatId)).ToListAsync(ct)
            : [];

        var branchNames = await db.Set<RvBranchRow>().AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.Name, ct);
        var careKinds = new[] { NotificationSubjectKind.CatVaccinationDue.Value, NotificationSubjectKind.CatDewormingDue.Value };
        var reminders = await db.Set<RvCareReminderSentRow>().AsNoTracking()
            .Where(r => careKinds.Contains(r.SubjectKind) && r.SubjectId != null && cats.Contains(r.SubjectId.Value)).ToListAsync(ct);

        var booked = AppointmentStatus.Booked.Value;
        var withNextAppointment = (await db.Set<RvClinicAppointmentRow>().AsNoTracking()
            .Where(a => cats.Contains(a.CatId) && a.Status == booked && a.StartsOn >= request.Now).Select(a => a.CatId).Distinct().ToListAsync(ct)).ToHashSet();

        var rows = VaccinationDueListBuilder.Build(
            new DueListInputs(vaccinations, treatments, lastBranchByCat, branchNames, reminders, withNextAppointment),
            request.From, request.To, today, request.BranchId, request.AllBranches, request.Flag("includeOverdue"), includeParasite, request.Flag("includeUnattributed"));

        return new ReportSourceResult(rows.Take(request.RowLimit).ToList(), new ReportRow { ["catName"] = $"{rows.Count} due" });
    }
}

internal static class ClinicQueries
{
    /// <summary>Appointments (never holds -- the view excludes them) whose start, or for a walk-in whose check-in, falls in the report's instant range at the report's branch.</summary>
    public static IQueryable<RvClinicAppointmentRow> AppointmentsInRange(CatEcommerceDbContext db, ReportRequest request)
    {
        var query = db.Set<RvClinicAppointmentRow>().AsNoTracking()
            .Where(a => (a.StartsOn ?? a.CheckedInOn) >= request.StartInclusive && (a.StartsOn ?? a.CheckedInOn) < request.EndExclusive);

        return request.AllBranches ? query : query.Where(a => a.BranchId == request.BranchId);
    }
}
