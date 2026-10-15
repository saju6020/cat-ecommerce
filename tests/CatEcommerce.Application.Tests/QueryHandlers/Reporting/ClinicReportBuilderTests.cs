using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.QueryHandlers.Reporting;

/// <summary>HO-RPT-001 section 15 B -- U-B1 to U-B5: the clinic reports' builders are pure over the view rows, so they are tested without a database.</summary>
public sealed class ClinicReportBuilderTests
{
    private static readonly TimeSpan Dhaka = TimeSpan.FromHours(6);
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly DateOnly Yesterday = new(2026, 10, 13);
    private static readonly Guid BranchA = Guid.NewGuid();
    private static readonly Guid BranchB = Guid.NewGuid();

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) => new(day.Year, day.Month, day.Day, hour, minute, 0, Dhaka);

    private static DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(Dhaka).DateTime);

    private static RvClinicAppointmentRow Appointment(
        AppointmentStatus status, DateOnly day, int hour = 9, bool walkIn = false, TriagePriority? triage = null, DateTimeOffset? checkedIn = null, DateTimeOffset? started = null, string cat = "Mimi") => new()
    {
        AppointmentId = Guid.NewGuid(), BranchId = BranchA, LocalDate = day, StartsOn = walkIn ? null : At(day, hour), CustomerName = "Owner", CatName = cat, ServiceName = "Consultation",
        Channel = (walkIn ? BookingChannel.WalkIn : BookingChannel.Reception).Value, IsWalkIn = walkIn, TriagePriority = (triage ?? TriagePriority.Routine).Value, Status = status.Value,
        CheckedInOn = checkedIn, StartedOn = started,
    };

    // ── U-B1: holds never appear; the totals count every status and the walk-ins ───────────────────────────
    [Fact]
    public void U_B1_HoldsNeverAppear_AndTheTotalsCountBookedCheckedInCompletedCancelledNoShowAndWalkIns()
    {
        var rows = new[]
        {
            Appointment(AppointmentStatus.Booked, Today, 9),
            Appointment(AppointmentStatus.Booked, Today, 10),
            Appointment(AppointmentStatus.CheckedIn, Today, 11, checkedIn: At(Today, 10, 55)),
            Appointment(AppointmentStatus.Completed, Today, 8, checkedIn: At(Today, 7, 50), started: At(Today, 8, 10)),
            Appointment(AppointmentStatus.Cancelled, Today, 12),
            Appointment(AppointmentStatus.NoShow, Today, 13),
            Appointment(AppointmentStatus.CheckedIn, Today, walkIn: true, checkedIn: At(Today, 9, 30), cat: "Walker"),
            Appointment(AppointmentStatus.Held, Today, 14),
            Appointment(AppointmentStatus.HoldExpired, Today, 15),
            Appointment(AppointmentStatus.HoldReleased, Today, 16),
        };

        var (output, totals) = ClinicScheduleBuilder.Build(rows);

        Assert.Equal(7, output.Count);
        Assert.DoesNotContain(output, r => (string?)r["status"] is "Held" or "HoldExpired" or "HoldReleased");
        Assert.Equal("7 appointments", totals["catName"]);
        Assert.Equal("Booked 2 · Checked in 2 · Completed 1 · Cancelled 1 · No-show 1 · Walk-ins 1", totals["status"]);

        // Booked cats first by start time, walk-ins by check-in time: 08:00 (completed), 09:00, 09:30 (walk-in), 10:00, 11:00, 12:00, 13:00.
        Assert.Equal(["Completed", "Booked", "CheckedIn", "Booked", "CheckedIn", "Cancelled", "NoShow"], output.Select(r => (string)r["status"]!));
        Assert.Equal(20L, output[0]["waitMinutes"]);
        Assert.Null(output[1]["waitMinutes"]); // not recorded: null, never 0
    }

    // ── U-B2 ──────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_B2_AWalkInCheckedIn0900AndStarted0935_Waited35_OneLeftWithoutBeingSeen_AndWaitingNowIsTodayOnly()
    {
        var rows = new[]
        {
            Appointment(AppointmentStatus.Completed, Today, walkIn: true, triage: TriagePriority.Urgent, checkedIn: At(Today, 9), started: At(Today, 9, 35)),
            Appointment(AppointmentStatus.Cancelled, Today, 11, checkedIn: At(Today, 10, 45)),
            Appointment(AppointmentStatus.CheckedIn, Today, 12, checkedIn: At(Today, 11, 50)),
            Appointment(AppointmentStatus.Completed, Yesterday, 9, checkedIn: At(Yesterday, 8, 55), started: At(Yesterday, 9, 5)),
            Appointment(AppointmentStatus.CheckedIn, Yesterday, 15, checkedIn: At(Yesterday, 14, 50)),
        };

        var (output, totals) = QueuePerformanceBuilder.Build(rows, Yesterday, Today, Today, LocalDate);

        var today = output.Single(r => (DateOnly)r["localDate"]! == Today);
        var yesterday = output.Single(r => (DateOnly)r["localDate"]! == Yesterday);

        Assert.Equal(35L, today["longestWaitMinutes"]);
        Assert.Equal(35m, today["averageWaitMinutes"]);
        Assert.Equal(1L, today["walkInsUrgent"]);
        Assert.Equal(1L, today["leftWithoutBeingSeen"]);
        Assert.Equal(2L, today["bookedCheckIns"]); // the cancelled one and the one still waiting
        Assert.Equal(1L, today["seen"]);
        Assert.Equal(1L, today["waitingNow"]);

        Assert.Null(yesterday["waitingNow"]); // other days show null, rendered as a dash -- not zero
        Assert.Equal(10L, yesterday["longestWaitMinutes"]);
        Assert.Equal(1L, totals["leftWithoutBeingSeen"]);
        Assert.Equal(35L, totals["longestWaitMinutes"]);
    }

    [Fact]
    public void U_B2_ADayWithNoAppointments_HasZeroCounts_AndNoWaitFigures()
    {
        var (output, _) = QueuePerformanceBuilder.Build([], Yesterday, Today, Today, LocalDate);

        Assert.Equal(2, output.Count);
        Assert.All(output, r => Assert.Null(r["averageWaitMinutes"]));
        Assert.All(output, r => Assert.Equal(0L, r["seen"]));
    }

    // ── U-B3 .. U-B5: the vaccination due list ────────────────────────────────────────────────────────────
    private static RvCatVaccinationRow Vaccination(
        Guid catId, string cat, Guid vaccineId, string vaccine, DateOnly given, DateOnly nextDue, int lifeStatus = 1, int ownerStatus = 1, bool active = true) => new()
    {
        CatId = catId, CatName = cat, OwnerId = Guid.NewGuid(), OwnerName = "Owner of " + cat, OwnerMobile = "01700000000", OwnerEmail = cat.ToLowerInvariant() + "@example.test",
        OwnerStatus = ownerStatus, CatLifeStatus = lifeStatus, VaccineId = vaccineId, VaccineName = vaccine, VaccineActive = active, AdministeredOn = given, NextDueOn = nextDue,
    };

    private static DueListInputs Inputs(
        IEnumerable<RvCatVaccinationRow> vaccinations, IEnumerable<RvCatParasiteTreatmentRow>? treatments = null, IDictionary<Guid, Guid>? lastBranch = null,
        IEnumerable<RvCareReminderSentRow>? reminders = null, IEnumerable<Guid>? nextAppointments = null) => new(
        vaccinations.ToList(), (treatments ?? []).ToList(), (lastBranch ?? new Dictionary<Guid, Guid>()).ToDictionary(k => k.Key, k => k.Value),
        new Dictionary<Guid, string> { [BranchA] = "Branch A", [BranchB] = "Branch B" }, (reminders ?? []).ToList(), (nextAppointments ?? []).ToHashSet());

    [Fact]
    public void U_B3_OnlyTheLatestRecordOfAVaccineCounts_ADeceasedCat_AMergedOwner_AndAnInactiveVaccine_AreExcluded()
    {
        var vaccine = Guid.NewGuid();
        var renewed = Guid.NewGuid();
        var deceased = Guid.NewGuid();
        var merged = Guid.NewGuid();
        var retired = Guid.NewGuid();
        var anonymised = Guid.NewGuid();
        var overdue = Guid.NewGuid();
        var from = Today;
        var to = Today.AddDays(30);

        var inputs = Inputs(
            [
                // The cat whose old record is overdue but who has since been renewed: the latest record is not due in the window.
                Vaccination(renewed, "Renewed", vaccine, "FVRCP", Today.AddYears(-2), Today.AddDays(-365)),
                Vaccination(renewed, "Renewed", vaccine, "FVRCP", Today.AddDays(-20), Today.AddDays(345)),
                // A cat due in 10 days, and one overdue.
                Vaccination(Guid.NewGuid(), "Soon", vaccine, "FVRCP", Today.AddDays(-355), Today.AddDays(10)),
                Vaccination(overdue, "Late", vaccine, "FVRCP", Today.AddDays(-400), Today.AddDays(-35)),
                Vaccination(deceased, "Gone", vaccine, "FVRCP", Today.AddDays(-355), Today.AddDays(10), lifeStatus: CatLifeStatus.Deceased.Value),
                Vaccination(merged, "OldOwner", vaccine, "FVRCP", Today.AddDays(-355), Today.AddDays(10), ownerStatus: CustomerAccountStatus.Merged.Value),
                Vaccination(anonymised, "Erased", vaccine, "FVRCP", Today.AddDays(-355), Today.AddDays(10), ownerStatus: CustomerAccountStatus.Anonymised.Value),
                Vaccination(retired, "Retired", Guid.NewGuid(), "Old vaccine", Today.AddDays(-355), Today.AddDays(10), active: false),
            ]);

        var rows = VaccinationDueListBuilder.Build(inputs, from, to, Today, null, allBranches: true, includeOverdue: true, includeParasiteControl: false, includeUnattributed: false);

        Assert.Equal(["Late", "Soon"], rows.Select(r => (string)r["catName"]!));
        Assert.Equal("Overdue", rows[0]["status"]);
        Assert.Equal("DueSoon", rows[1]["status"]);
        Assert.Equal("01700000000", rows[1]["mobile"]);
        Assert.Equal("soon@example.test", rows[1]["email"]);

        // includeOverdue = false drops the overdue cat only.
        var onlySoon = VaccinationDueListBuilder.Build(inputs, from, to, Today, null, true, includeOverdue: false, false, false);
        Assert.Equal(["Soon"], onlySoon.Select(r => (string)r["catName"]!));
    }

    [Fact]
    public void U_B3_TheLastReminderSent_AndTheNextAppointmentBooked_AreFilled()
    {
        var cat = Guid.NewGuid();
        var vaccine = Guid.NewGuid();
        var due = Today.AddDays(5);
        var sent = At(Today, 7);

        var rows = VaccinationDueListBuilder.Build(
            Inputs([Vaccination(cat, "Cleo", vaccine, "FVRCP", Today.AddDays(-360), due)],
                reminders: [new RvCareReminderSentRow { SubjectKind = NotificationSubjectKind.CatVaccinationDue.Value, SubjectId = cat, SubjectRef = vaccine, SubjectDueOn = due, SentOn = sent }],
                nextAppointments: [cat]),
            Today, Today.AddDays(30), Today, null, true, true, false, false);

        Assert.Equal(sent, rows.Single()["lastReminderSentOn"]);
        Assert.Equal(true, rows.Single()["nextAppointmentBooked"]);
    }

    // ── U-B4: a cat belongs to the branch of its most recent appointment (ADR-292) ─────────────────────────
    [Fact]
    public void U_B4_ACatLastSeenAtBranchB_IsListedUnderB_ACatNeverSeen_IsUnattributed_UnlessAllOrOptedIn()
    {
        var seenAtB = Guid.NewGuid();
        var seenAtA = Guid.NewGuid();
        var neverSeen = Guid.NewGuid();
        var vaccine = Guid.NewGuid();
        var vaccinations = new[]
        {
            Vaccination(seenAtB, "AtB", vaccine, "FVRCP", Today.AddDays(-350), Today.AddDays(5)),
            Vaccination(seenAtA, "AtA", vaccine, "FVRCP", Today.AddDays(-350), Today.AddDays(6)),
            Vaccination(neverSeen, "Never", vaccine, "FVRCP", Today.AddDays(-350), Today.AddDays(7)),
        };
        var lastBranch = new Dictionary<Guid, Guid> { [seenAtB] = BranchB, [seenAtA] = BranchA };

        IReadOnlyList<string> Names(Guid? branch, bool all, bool includeUnattributed) =>
            VaccinationDueListBuilder.Build(Inputs(vaccinations, lastBranch: lastBranch), Today, Today.AddDays(30), Today, branch, all, true, false, includeUnattributed)
                .Select(r => (string)r["catName"]!).ToList();

        Assert.Equal(["AtB"], Names(BranchB, false, false));
        Assert.Equal(["AtA"], Names(BranchA, false, false));
        Assert.Equal(["AtA", "Never"], Names(BranchA, false, true));
        Assert.Equal(["AtB", "AtA", "Never"], Names(null, true, false));

        var all = VaccinationDueListBuilder.Build(Inputs(vaccinations, lastBranch: lastBranch), Today, Today.AddDays(30), Today, null, true, true, false, false);
        Assert.Equal(VaccinationDueListBuilder.Unattributed, all.Single(r => (string)r["catName"]! == "Never")["branchName"]);
        Assert.Equal("Branch B", all.Single(r => (string)r["catName"]! == "AtB")["branchName"]);
    }

    // ── U-B5: a Combined treatment counts as deworming (HO-NTF-001 F25) ───────────────────────────────────
    [Fact]
    public void U_B5_IncludeParasiteControl_AddsDewormingRows_AndACombinedTreatmentCountsAsDeworming_FleaOnlyDoesNot()
    {
        var combined = Guid.NewGuid();
        var fleaOnly = Guid.NewGuid();
        RvCatParasiteTreatmentRow Treatment(Guid cat, string name, ParasiteTreatmentType type, DateOnly due) => new()
        {
            CatId = cat, CatName = name, OwnerName = "Owner", OwnerStatus = 1, CatLifeStatus = 1, TreatmentType = type.Value, ProductName = "Product", GivenOn = due.AddDays(-90), NextDueOn = due,
        };

        var treatments = new[] { Treatment(combined, "Combo", ParasiteTreatmentType.Combined, Today.AddDays(4)), Treatment(fleaOnly, "Fleas", ParasiteTreatmentType.FleaAndTick, Today.AddDays(4)) };

        var without = VaccinationDueListBuilder.Build(Inputs([], treatments), Today, Today.AddDays(30), Today, null, true, true, includeParasiteControl: false, false);
        var with = VaccinationDueListBuilder.Build(Inputs([], treatments), Today, Today.AddDays(30), Today, null, true, true, includeParasiteControl: true, false);

        Assert.Empty(without);
        Assert.Equal(["Combo"], with.Select(r => (string)r["catName"]!));
        Assert.StartsWith("Deworming", (string)with.Single()["item"]!);
    }

    // ── R3 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void R3_TheRegister_ClassifiesSpayNeuterOther_AndHidesTheComplicationsTextWhenThereWereNone()
    {
        RvSurgeryRow Surgery(ServiceCategory category, bool complications, string? summary) => new()
        {
            ProcedureName = category.DisplayName, ProcedureCategory = category.Value, LocalDate = Today, OccurredOn = At(Today, 9), CatName = "Mimi", CustomerName = "Owner", SurgeonName = "Dr A",
            Status = SurgeryStatus.Discharged.Value, Outcome = ProcedureOutcome.Completed.Value, AsaGrade = AsaGrade.II.Value, HasComplications = complications, ComplicationsSummary = summary, ControlledDrugEntries = 2,
        };

        var (rows, totals) = SurgeryRegisterBuilder.Build([Surgery(ServiceCategory.Spay, true, "Bleeding"), Surgery(ServiceCategory.Neuter, false, "stale text"), Surgery(ServiceCategory.Dental, false, null)]);

        Assert.Equal(["Spay", "Neuter", "Other"], rows.Select(r => (string)r["procedureCategory"]!));
        Assert.Equal("Bleeding", rows[0]["complicationsSummary"]);
        Assert.Null(rows[1]["complicationsSummary"]);
        Assert.Equal("II", rows[0]["asaGrade"]);
        Assert.Equal("Completed", rows[0]["outcome"]);
        Assert.Equal("3 procedures", totals["procedureName"]);
        Assert.Equal("1 with complications", totals["complicationsSummary"]);
        Assert.Equal(6L, totals["controlledDrugEntries"]);
    }
}
