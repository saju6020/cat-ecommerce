using System.Globalization;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>
/// HO-RPT-001 section 8.3 R1 -- the daily clinic schedule, <b>pure</b> over the rows of <c>rv_clinic_appointment</c> (which already excludes the three hold statuses in its WHERE; the builder
/// refuses them again, so a future view change cannot let a hold through). Walk-ins have no start, so they sort by check-in time.
/// </summary>
public static class ClinicScheduleBuilder
{
    private static readonly int[] HoldStatuses = [AppointmentStatus.Held.Value, AppointmentStatus.HoldExpired.Value, AppointmentStatus.HoldReleased.Value];

    public static (IReadOnlyList<ReportRow> Rows, ReportRow Totals) Build(IEnumerable<RvClinicAppointmentRow> source)
    {
        var rows = source.Where(a => !HoldStatuses.Contains(a.Status))
            .OrderBy(a => a.StartsOn ?? a.CheckedInOn).ThenBy(a => a.CheckedInOn).ThenBy(a => a.CatName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var output = rows.Select(a => new ReportRow
        {
            ["localDate"] = a.LocalDate,
            ["startsOn"] = a.StartsOn,
            ["catName"] = a.CatName,
            ["customerName"] = a.CustomerName,
            ["serviceName"] = a.ServiceName,
            ["performerName"] = a.PerformerName,
            ["roomName"] = a.RoomName,
            ["channel"] = ReportLabels.Of<BookingChannel>(a.Channel),
            ["walkIn"] = a.IsWalkIn,
            ["status"] = ReportLabels.Of<AppointmentStatus>(a.Status),
            ["checkedInOn"] = a.CheckedInOn,
            ["startedOn"] = a.StartedOn,
            ["completedOn"] = a.CompletedOn,
            ["waitMinutes"] = WaitMinutes(a.CheckedInOn, a.StartedOn),
        }).ToList<ReportRow>();

        int Count(params AppointmentStatus[] statuses) => rows.Count(a => statuses.Any(s => s.Value == a.Status));

        var totals = new ReportRow
        {
            ["catName"] = $"{rows.Count} appointments",
            ["status"] = string.Join(" · ",
            [
                $"Booked {Count(AppointmentStatus.Booked)}",
                $"Checked in {Count(AppointmentStatus.CheckedIn, AppointmentStatus.InConsultation)}",
                $"Completed {Count(AppointmentStatus.Completed)}",
                $"Cancelled {Count(AppointmentStatus.Cancelled)}",
                $"No-show {Count(AppointmentStatus.NoShow)}",
                $"Walk-ins {rows.Count(a => a.IsWalkIn)}",
            ]),
        };

        return (output, totals);
    }

    /// <summary>Minutes from check-in to the start of the consultation, or null when either is not recorded (never 0).</summary>
    public static long? WaitMinutes(DateTimeOffset? checkedInOn, DateTimeOffset? startedOn) =>
        checkedInOn is { } checkedIn && startedOn is { } started
            ? (long)Math.Round((started - checkedIn).TotalMinutes, MidpointRounding.AwayFromZero)
            : null;
}

/// <summary>
/// HO-RPT-001 section 8.3 R2 -- the per-day queue summary, <b>pure</b>. Wait = <c>started_on - checked_in_on</c>; "left without being seen" = checked in, then cancelled; "waiting now" is filled for
/// today only -- every other day is <c>null</c>, rendered as a dash, not zero. The live ordered queue is HO-APT-001's screen, not this report.
/// </summary>
public static class QueuePerformanceBuilder
{
    public static (IReadOnlyList<ReportRow> Rows, ReportRow Totals) Build(IEnumerable<RvClinicAppointmentRow> source, DateOnly from, DateOnly to, DateOnly today, Func<DateTimeOffset, DateOnly> localDate)
    {
        var byDay = source
            .Select(a => (Row: a, Day: (a.StartsOn ?? a.CheckedInOn) is { } instant ? localDate(instant) : (DateOnly?)null))
            .Where(x => x.Day is not null)
            .GroupBy(x => x.Day!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Row).ToList());

        var rows = new List<ReportRow>();
        var allWaits = new List<long>();
        var totalsCounts = new long[7];
        long? totalWaitingNow = null;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var items = byDay.GetValueOrDefault(day) ?? [];
            var walkIns = items.Where(a => a.IsWalkIn).ToList();
            var waits = items.Select(a => ClinicScheduleBuilder.WaitMinutes(a.CheckedInOn, a.StartedOn)).Where(w => w is not null).Select(w => w!.Value).ToList();
            allWaits.AddRange(waits);

            long Triage(TriagePriority p) => walkIns.Count(a => a.TriagePriority == p.Value);
            var bookedCheckIns = items.Count(a => !a.IsWalkIn && a.CheckedInOn is not null);
            var seen = items.Count(a => a.StartedOn is not null);
            var left = items.Count(a => a.CheckedInOn is not null && a.Status == AppointmentStatus.Cancelled.Value);
            long? waitingNow = day == today ? items.Count(a => a.CheckedInOn is not null && a.StartedOn is null && a.Status == AppointmentStatus.CheckedIn.Value) : null;

            rows.Add(new ReportRow
            {
                ["localDate"] = day,
                ["walkInsRoutine"] = Triage(TriagePriority.Routine),
                ["walkInsUrgent"] = Triage(TriagePriority.Urgent),
                ["walkInsEmergency"] = Triage(TriagePriority.Emergency),
                ["bookedCheckIns"] = (long)bookedCheckIns,
                ["seen"] = (long)seen,
                ["waitingNow"] = waitingNow,
                ["averageWaitMinutes"] = waits.Count == 0 ? null : (decimal?)decimal.Round((decimal)waits.Average(), 1, MidpointRounding.AwayFromZero),
                ["longestWaitMinutes"] = waits.Count == 0 ? null : (long?)waits.Max(),
                ["leftWithoutBeingSeen"] = (long)left,
            });

            totalsCounts[0] += Triage(TriagePriority.Routine);
            totalsCounts[1] += Triage(TriagePriority.Urgent);
            totalsCounts[2] += Triage(TriagePriority.Emergency);
            totalsCounts[3] += bookedCheckIns;
            totalsCounts[4] += seen;
            totalsCounts[5] += left;
            if (waitingNow is not null) totalWaitingNow = (totalWaitingNow ?? 0) + waitingNow;
        }

        var totals = new ReportRow
        {
            ["walkInsRoutine"] = totalsCounts[0],
            ["walkInsUrgent"] = totalsCounts[1],
            ["walkInsEmergency"] = totalsCounts[2],
            ["bookedCheckIns"] = totalsCounts[3],
            ["seen"] = totalsCounts[4],
            ["waitingNow"] = totalWaitingNow,
            ["averageWaitMinutes"] = allWaits.Count == 0 ? null : (decimal?)decimal.Round((decimal)allWaits.Average(), 1, MidpointRounding.AwayFromZero),
            ["longestWaitMinutes"] = allWaits.Count == 0 ? null : (long?)allWaits.Max(),
            ["leftWithoutBeingSeen"] = totalsCounts[5],
        };

        return (rows, totals);
    }
}

/// <summary>HO-RPT-001 section 8.3 R3 -- the surgery register, <b>pure</b> over <c>rv_surgery</c>. The category is Spay, Neuter or Other.</summary>
public static class SurgeryRegisterBuilder
{
    public static string CategoryOf(int procedureCategory) =>
        procedureCategory == ServiceCategory.Spay.Value ? "Spay" : procedureCategory == ServiceCategory.Neuter.Value ? "Neuter" : "Other";

    public static (IReadOnlyList<ReportRow> Rows, ReportRow Totals) Build(IEnumerable<RvSurgeryRow> source)
    {
        var rows = source.OrderBy(s => s.OccurredOn).ThenBy(s => s.CatName, StringComparer.OrdinalIgnoreCase).ToList();

        var output = rows.Select(s => new ReportRow
        {
            ["date"] = s.LocalDate,
            ["procedureName"] = s.ProcedureName,
            ["procedureCategory"] = CategoryOf(s.ProcedureCategory),
            ["catName"] = s.CatName,
            ["customerName"] = s.CustomerName,
            ["surgeonName"] = s.SurgeonName,
            ["assistantName"] = s.AssistantName,
            ["theatreName"] = s.TheatreName,
            ["asaGrade"] = ReportLabels.Of<AsaGrade>(s.AsaGrade),
            ["status"] = ReportLabels.Of<SurgeryStatus>(s.Status),
            ["outcome"] = ReportLabels.Of<ProcedureOutcome>(s.Outcome),
            ["hadComplications"] = s.HasComplications,
            ["complicationsSummary"] = s.HasComplications ? s.ComplicationsSummary : null,
            ["consentOverride"] = s.ConsentOverride,
            ["controlledDrugEntries"] = (long)s.ControlledDrugEntries,
        }).ToList<ReportRow>();

        var totals = new ReportRow
        {
            ["procedureName"] = $"{rows.Count} procedures",
            ["complicationsSummary"] = $"{rows.Count(s => s.HasComplications)} with complications",
            ["controlledDrugEntries"] = rows.Sum(s => (long)s.ControlledDrugEntries),
        };

        return (output, totals);
    }
}

/// <summary>What the vaccination due list needs about one cat's care items: the vaccination records and the parasite-control treatments, with the owner and the attribution.</summary>
public sealed record DueListInputs(
    IReadOnlyList<RvCatVaccinationRow> Vaccinations,
    IReadOnlyList<RvCatParasiteTreatmentRow> Treatments,
    IReadOnlyDictionary<Guid, Guid> LastBranchByCat,
    IReadOnlyDictionary<Guid, string> BranchNames,
    IReadOnlyList<RvCareReminderSentRow> RemindersSent,
    IReadOnlySet<Guid> CatsWithNextAppointment);

/// <summary>
/// HO-RPT-001 section 8.3 R4 (ADR-292) -- the vaccination due list, <b>pure</b>. The latest record per vaccine is chosen and its status judged by HO-CAT-001's own
/// <see cref="Cat.CatVaccinationStatusCalculator"/> -- never re-derived. Excludes deceased cats, owners that are Merged or Anonymised, and inactive vaccines. A cat belongs to the branch of its
/// most recent appointment; a cat never seen is "Unattributed" and listed only for all branches or with <c>includeUnattributed</c>.
/// </summary>
public static class VaccinationDueListBuilder
{
    public const string Unattributed = "Unattributed";

    public static IReadOnlyList<ReportRow> Build(
        DueListInputs inputs, DateOnly from, DateOnly to, DateOnly today, Guid? branchId, bool allBranches, bool includeOverdue, bool includeParasiteControl, bool includeUnattributed)
    {
        var windowDays = Math.Max(0, to.DayNumber - today.DayNumber);
        var rows = new List<(DateOnly NextDue, ReportRow Row)>();

        bool Attributed(Guid catId, out string branchName)
        {
            if (inputs.LastBranchByCat.TryGetValue(catId, out var lastBranch))
            {
                branchName = inputs.BranchNames.GetValueOrDefault(lastBranch) ?? string.Empty;
                return allBranches || lastBranch == branchId;
            }

            branchName = Unattributed;
            return allBranches || includeUnattributed;
        }

        bool Included(DateOnly nextDue, VaccinationStatus status) =>
            nextDue <= to && ((status == VaccinationStatus.DueSoon && nextDue >= from) || (status == VaccinationStatus.Overdue && includeOverdue && (nextDue >= from || nextDue < today)));

        // ── Vaccinations ────────────────────────────────────────────────────────────────────────────
        var eligible = inputs.Vaccinations.Where(v =>
            v.CatLifeStatus == CatLifeStatus.Alive.Value && v.OwnerStatus == CustomerAccountStatus.Active.Value && v.VaccineActive);

        foreach (var cat in eligible.GroupBy(v => v.CatId))
        {
            if (!Attributed(cat.Key, out var branchName)) continue;

            var records = cat.ToList();
            var vaccines = records.GroupBy(r => r.VaccineId).Select(g => new Cat.ActiveCoreVaccine(g.Key, g.First().VaccineName, g.First().VaccineName)).ToList();
            var catRecords = records.Select(r => new Cat.CatVaccinationRecord(r.VaccineId, r.AdministeredOn, r.NextDueOn)).ToList();

            var status = Cat.CatVaccinationStatusCalculator.Calculate(cat.Key, vaccines, catRecords, today, windowDays);
            foreach (var item in status.Vaccines)
            {
                if (item.NextDueOn is not { } nextDue) continue;
                var itemStatus = Enumeration.FromDisplayName<VaccinationStatus>(item.Status);
                if (!Included(nextDue, itemStatus)) continue;

                var latest = records.Where(r => r.VaccineId == item.VaccineId).OrderByDescending(r => r.AdministeredOn).First();
                var reminder = inputs.RemindersSent
                    .Where(r => r.SubjectKind == NotificationSubjectKind.CatVaccinationDue.Value && r.SubjectId == cat.Key && r.SubjectRef == latest.VaccineId && r.SubjectDueOn == nextDue)
                    .Select(r => (DateTimeOffset?)r.SentOn).OrderByDescending(s => s).FirstOrDefault();

                rows.Add((nextDue, DueRow(latest.CatName, latest.OwnerName, latest.OwnerMobile, latest.OwnerEmail, latest.VaccineName, latest.AdministeredOn, nextDue, itemStatus, reminder,
                    inputs.CatsWithNextAppointment.Contains(cat.Key), branchName)));
            }
        }

        // ── Parasite control: the latest deworming (a Combined treatment counts as deworming) per cat ──
        if (includeParasiteControl)
        {
            var dewormings = inputs.Treatments
                .Where(t => t.CatLifeStatus == CatLifeStatus.Alive.Value && t.OwnerStatus == CustomerAccountStatus.Active.Value
                            && (t.TreatmentType == ParasiteTreatmentType.Deworming.Value || t.TreatmentType == ParasiteTreatmentType.Combined.Value))
                .GroupBy(t => t.CatId);

            foreach (var cat in dewormings)
            {
                if (!Attributed(cat.Key, out var branchName)) continue;

                var latest = cat.OrderByDescending(t => t.GivenOn).First();
                var itemStatus = latest.NextDueOn < today ? VaccinationStatus.Overdue : latest.NextDueOn <= today.AddDays(windowDays) ? VaccinationStatus.DueSoon : VaccinationStatus.Current;
                if (!Included(latest.NextDueOn, itemStatus)) continue;

                var reminder = inputs.RemindersSent
                    .Where(r => r.SubjectKind == NotificationSubjectKind.CatDewormingDue.Value && r.SubjectId == cat.Key && r.SubjectDueOn == latest.NextDueOn)
                    .Select(r => (DateTimeOffset?)r.SentOn).OrderByDescending(s => s).FirstOrDefault();

                rows.Add((latest.NextDueOn, DueRow(latest.CatName, latest.OwnerName, latest.OwnerMobile, latest.OwnerEmail, "Deworming: " + latest.ProductName, latest.GivenOn, latest.NextDueOn, itemStatus, reminder,
                    inputs.CatsWithNextAppointment.Contains(cat.Key), branchName)));
            }
        }

        return rows.OrderBy(r => r.NextDue).ThenBy(r => (string?)r.Row["catName"], StringComparer.OrdinalIgnoreCase).Select(r => r.Row).ToList();
    }

    private static ReportRow DueRow(
        string catName, string customerName, string? mobile, string? email, string item, DateOnly lastGiven, DateOnly nextDue, VaccinationStatus status, DateTimeOffset? reminder,
        bool nextAppointmentBooked, string branchName) => new()
    {
        ["catName"] = catName,
        ["customerName"] = customerName,
        ["mobile"] = mobile,
        ["email"] = email,
        ["item"] = item,
        ["lastGivenOn"] = lastGiven,
        ["nextDueOn"] = nextDue,
        ["status"] = status.DisplayName,
        ["lastReminderSentOn"] = reminder,
        ["nextAppointmentBooked"] = nextAppointmentBooked,
        ["branchName"] = branchName,
    };
}
