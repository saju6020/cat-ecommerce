namespace CatEcommerce.Read.ViewModels;

// HO-RPT-001 section 12.4 -- the schedule screens' shapes. Flat records with init-only properties (no ViewModelBase).

public sealed record ReportScheduleListItemViewModel(
    Guid Id, string Name, string ReportKey, string ReportTitle, string BranchName, string FrequencyLabel, string SendAtLocal, string TimeZoneId, string Format,
    int RecipientCount, string Status, DateTimeOffset? NextRunOn, DateTimeOffset? LastRunOn, string? LastRunStatus);

public sealed record ReportScheduleRecipientViewModel(Guid Id, string Name, bool EmailPresent, bool StillPermitted);

public sealed record ReportScheduleDetailViewModel(
    Guid Id, int Version, string Name, string ReportKey, string ReportTitle, Guid? BranchId, string BranchName, string Period, IReadOnlyDictionary<string, string> Parameters,
    string Format, string Frequency, string? DayOfWeek, int? DayOfMonth, bool LastDayOfMonth, string FrequencyLabel, string SendAtLocal, string TimeZoneId,
    IReadOnlyList<ReportScheduleRecipientViewModel> Recipients, string Status, DateTimeOffset? NextRunOn, DateTimeOffset? LastRunOn);

public sealed record ReportScheduleDeliveryViewModel(string RecipientName, string Status);

public sealed record ReportScheduleRunViewModel(
    Guid RunId, DateTimeOffset DueOn, string Trigger, string Status, DateOnly PeriodFrom, DateOnly PeriodTo, string Format, int RowCount, long ContentLength,
    bool AttachmentOmitted, string? FailureCode, DateTimeOffset GeneratedOn, DateTimeOffset? PurgedOn, IReadOnlyList<ReportScheduleDeliveryViewModel> Deliveries);

public sealed record EligibleReportRecipientViewModel(Guid Id, string Name, IReadOnlyList<string> Roles, bool EmailPresent);

public sealed record ReportSchedulePreviewPeriodViewModel(DateOnly From, DateOnly To);

public sealed record ReportSchedulePreviewViewModel(IReadOnlyList<DateTimeOffset> NextRuns, ReportSchedulePreviewPeriodViewModel PeriodForNextRun, DateTimeOffset? DeliveryHeldUntil);

public sealed record ReportSchedulePreviewOutcome(ReportSchedulePreviewViewModel? Result, string? RefusalCode, string? RefusalMessage, string? RefusalProperty)
{
    public bool IsRefused => RefusalCode is not null;

    public static ReportSchedulePreviewOutcome Success(ReportSchedulePreviewViewModel result) => new(result, null, null, null);

    public static ReportSchedulePreviewOutcome Refused(string code, string message, string property) => new(null, code, message, property);
}

public sealed record DueReportScheduleViewModel(Guid ScheduleId, DateTimeOffset DueOn);

public sealed record ReportSchedulePlanRecipientViewModel(Guid Id, IReadOnlyList<string> Roles);

public sealed record ReportSchedulePlanViewModel(
    Guid ScheduleId, string ReportKey, Guid? BranchId, string Period, IReadOnlyDictionary<string, string> Parameters, string Format, string TimeZoneId, string Status, DateTimeOffset? NextRunOn,
    IReadOnlyList<ReportSchedulePlanRecipientViewModel> Recipients, string Frequency, DayOfWeek? DayOfWeek, int? DayOfMonth, bool LastDayOfMonth, TimeOnly SendAtLocal);
