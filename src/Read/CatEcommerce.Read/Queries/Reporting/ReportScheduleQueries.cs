using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Reporting;

// HO-RPT-001 section 12.4 / 12.6 -- the schedule screens' reads. They read Reporting's own two tables (and the notification rows for delivery status), never the report.rv_* views.

/// <summary>The schedules list: <c>status</c> and <c>reportKey</c> filters, by name.</summary>
public sealed class GetReportSchedulesQuery : Query<PagedResult<ReportScheduleListItemViewModel>>
{
    public string? Status { get; init; }
    public string? ReportKey { get; init; }
}

/// <summary>One schedule in full, with its recipients and whether each is still permitted.</summary>
public sealed class GetReportScheduleQuery : Query<ReportScheduleDetailViewModel?>
{
    public required Guid ScheduleId { get; init; }
}

/// <summary>A schedule's run history, newest first, each with its per-recipient delivery status (read from the notification rows).</summary>
public sealed class GetReportScheduleRunsQuery : Query<PagedResult<ReportScheduleRunViewModel>?>
{
    public required Guid ScheduleId { get; init; }
}

/// <summary>The staff who could be chosen as recipients: only those who hold the report's action (and the branch).</summary>
public sealed class GetEligibleReportRecipientsQuery : Query<IReadOnlyList<EligibleReportRecipientViewModel>?>
{
    public required string ReportKey { get; init; }
    public Guid? BranchId { get; init; }
}

/// <summary>A draft schedule: when it would next run, the period that run would cover, and when quiet hours would hold its delivery. Writes nothing; the client never computes a schedule.</summary>
public sealed class PreviewReportScheduleQuery : Query<ReportSchedulePreviewOutcome>
{
    public required string ReportKey { get; init; }
    public Guid? BranchId { get; init; }
    public required ReportRelativePeriod Period { get; init; }
    public required ReportFrequency Frequency { get; init; }
    public DayOfWeek? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public bool LastDayOfMonth { get; init; }
    public required TimeOnly SendAtLocal { get; init; }
}

/// <summary>The worker's first step: the active schedules whose next run is due, oldest first, at most <see cref="Limit"/>.</summary>
public sealed class GetDueReportSchedulesQuery : Query<IReadOnlyList<DueReportScheduleViewModel>>
{
    public required DateTimeOffset Now { get; init; }
    public int Limit { get; init; } = 20;
}

/// <summary>What the worker needs to run one schedule: its fields, its zone, and each recipient's roles (to work out the columns every recipient may see).</summary>
public sealed class GetReportSchedulePlanQuery : Query<ReportSchedulePlanViewModel?>
{
    public required Guid ScheduleId { get; init; }
}

/// <summary>How many failed runs a slot already has (the worker advances a slot after <c>MaxAttemptsPerSlot</c>).</summary>
public sealed class CountFailedReportRunsQuery : Query<int>
{
    public required Guid ScheduleId { get; init; }
    public required DateTimeOffset DueOn { get; init; }
}
