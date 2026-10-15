using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 8.3 phase B -- R1 to R4: the clinic operations and clinical reports (US-RPT-01 AC1).</summary>
public static partial class ReportCatalogue
{
    public const string ClinicDailyScheduleKey = "clinic-daily-schedule";
    public const string ClinicQueuePerformanceKey = "clinic-queue-performance";
    public const string SurgeryRegisterKey = "surgery-register";
    public const string VaccinationDueListKey = "vaccination-due-list";

    private static readonly IReadOnlyList<ReportDefinition> ClinicDefinitions =
    [
        // R1 -- holds never appear; walk-ins sort by check-in time.
        new ReportDefinition(
            ClinicDailyScheduleKey, "Daily clinic schedule",
            "Every appointment and walk-in of the branch over the date range, with its status and waiting time. Held slots never appear.",
            ReportCategory.ClinicOperations, ReportKind.Period, ReportRelativePeriod.Today, 31, [],
            [
                new ReportColumn("localDate", "Date", ReportColumnType.Date),
                new ReportColumn("startsOn", "Start", ReportColumnType.Instant),
                new ReportColumn("catName", "Cat", ReportColumnType.Text),
                new ReportColumn("customerName", "Customer", ReportColumnType.Text),
                new ReportColumn("serviceName", "Service", ReportColumnType.Text),
                new ReportColumn("performerName", "Performer", ReportColumnType.Text),
                new ReportColumn("roomName", "Room", ReportColumnType.Text),
                new ReportColumn("channel", "Channel", ReportColumnType.Text),
                new ReportColumn("walkIn", "Walk-in", ReportColumnType.Boolean),
                new ReportColumn("status", "Status", ReportColumnType.Text),
                new ReportColumn("checkedInOn", "Checked in", ReportColumnType.Instant),
                new ReportColumn("startedOn", "Started", ReportColumnType.Instant),
                new ReportColumn("completedOn", "Completed", ReportColumnType.Instant),
                new ReportColumn("waitMinutes", "Wait (min)", ReportColumnType.Integer, "min"),
            ],
            null, true, false, ["APT", "CAT", "ADM"]),

        // R2 -- the per-day summary of the queue; the live ordered queue is HO-APT-001's screen.
        new ReportDefinition(
            ClinicQueuePerformanceKey, "Queue performance",
            "Per day: walk-ins by triage, booked check-ins, cats seen, waiting times and cats who left without being seen. Waiting now is filled for today only.",
            ReportCategory.ClinicOperations, ReportKind.Period, ReportRelativePeriod.Today, 366, [],
            [
                new ReportColumn("localDate", "Date", ReportColumnType.Date),
                new ReportColumn("walkInsRoutine", "Walk-ins: routine", ReportColumnType.Integer),
                new ReportColumn("walkInsUrgent", "Walk-ins: urgent", ReportColumnType.Integer),
                new ReportColumn("walkInsEmergency", "Walk-ins: emergency", ReportColumnType.Integer),
                new ReportColumn("bookedCheckIns", "Booked check-ins", ReportColumnType.Integer),
                new ReportColumn("seen", "Seen", ReportColumnType.Integer),
                new ReportColumn("waitingNow", "Waiting now", ReportColumnType.Integer),
                new ReportColumn("averageWaitMinutes", "Average wait (min)", ReportColumnType.Decimal, "min"),
                new ReportColumn("longestWaitMinutes", "Longest wait (min)", ReportColumnType.Integer, "min"),
                new ReportColumn("leftWithoutBeingSeen", "Left without being seen", ReportColumnType.Integer),
            ],
            "localDate", true, false, ["APT"]),

        // R3 -- clinical text is Clinical-only (NFR-SEC-8): the category itself is the gate.
        new ReportDefinition(
            SurgeryRegisterKey, "Surgery register",
            "Procedures by date, surgeon, outcome and complications, with the controlled-drug entries written for each.",
            ReportCategory.Clinical, ReportKind.Period, ReportRelativePeriod.Last7Days, 366, [],
            [
                new ReportColumn("date", "Date", ReportColumnType.Date),
                new ReportColumn("procedureName", "Procedure", ReportColumnType.Text),
                new ReportColumn("procedureCategory", "Category", ReportColumnType.Text),
                new ReportColumn("catName", "Cat", ReportColumnType.Text),
                new ReportColumn("customerName", "Customer", ReportColumnType.Text),
                new ReportColumn("surgeonName", "Surgeon", ReportColumnType.Text),
                new ReportColumn("assistantName", "Assistant", ReportColumnType.Text),
                new ReportColumn("theatreName", "Theatre", ReportColumnType.Text),
                new ReportColumn("asaGrade", "ASA grade", ReportColumnType.Text),
                new ReportColumn("status", "Status", ReportColumnType.Text),
                new ReportColumn("outcome", "Outcome", ReportColumnType.Text),
                new ReportColumn("hadComplications", "Complications", ReportColumnType.Boolean),
                new ReportColumn("complicationsSummary", "Complications summary", ReportColumnType.Text, Sortable: false),
                new ReportColumn("consentOverride", "Consent override", ReportColumnType.Boolean),
                new ReportColumn("controlledDrugEntries", "Controlled-drug entries", ReportColumnType.Integer),
            ],
            "date", true, false, ["SRG", "PHM"]),

        // R4 -- the due window; cats belong to the branch of their most recent appointment (ADR-292).
        new ReportDefinition(
            VaccinationDueListKey, "Vaccination due list",
            "Cats whose vaccination (and, if chosen, parasite control) falls due in the window, or is already overdue, with the owner's contact details.",
            ReportCategory.ClinicOperations, ReportKind.Period, ReportRelativePeriod.Next30Days, 366,
            [
                new ReportParameter("includeOverdue", "Include overdue", ReportParameterType.Boolean, "true"),
                new ReportParameter("includeParasiteControl", "Include parasite control", ReportParameterType.Boolean, "false"),
                new ReportParameter("includeUnattributed", "Include cats never seen at a branch", ReportParameterType.Boolean, "false"),
            ],
            [
                new ReportColumn("catName", "Cat", ReportColumnType.Text),
                new ReportColumn("customerName", "Customer", ReportColumnType.Text),
                new ReportColumn("mobile", "Mobile", ReportColumnType.Text, Sortable: false),
                new ReportColumn("email", "Email", ReportColumnType.Text, Sortable: false),
                new ReportColumn("item", "Vaccine or treatment", ReportColumnType.Text),
                new ReportColumn("lastGivenOn", "Last given", ReportColumnType.Date),
                new ReportColumn("nextDueOn", "Next due", ReportColumnType.Date),
                new ReportColumn("status", "Status", ReportColumnType.Text),
                new ReportColumn("lastReminderSentOn", "Last reminder sent", ReportColumnType.Instant),
                new ReportColumn("nextAppointmentBooked", "Next appointment booked", ReportColumnType.Boolean),
                new ReportColumn("branchName", "Branch", ReportColumnType.Text),
            ],
            "nextDueOn", true, false, ["CAT", "APT", "NTF"]),
    ];
}
