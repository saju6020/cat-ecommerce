using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Reporting;

// HO-RPT-001 section 11.1 (C2-C7) -- data only; ids are minted by the controller or the worker. ValidateCommand = true everywhere (HO-ACC-001 F3).

/// <summary>The fields C2 and C3 share. The report, parameters, period, branch and every recipient are checked in the handler.</summary>
public abstract class ReportScheduleCommandBase : Command
{
    public required Guid ScheduleId { get; init; }
    public required string Name { get; init; }
    public required string ReportKey { get; init; }

    /// <summary>A branch id, or null for all branches (only for a caller who may run reports across branches).</summary>
    public Guid? BranchId { get; init; }

    public required ReportRelativePeriod Period { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public required ReportFormat Format { get; init; }
    public required ReportFrequency Frequency { get; init; }
    public DayOfWeek? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public bool LastDayOfMonth { get; init; }
    public required TimeOnly SendAtLocal { get; init; }
    public required IReadOnlyList<Guid> RecipientIds { get; init; }
}

/// <summary>C2 -- the administrator creates a schedule.</summary>
public sealed class CreateReportScheduleCommand : ReportScheduleCommandBase;

/// <summary>C3 -- change a schedule. <c>ExpectedVersion</c> is the version the editor loaded; a stale one is a 409.</summary>
public sealed class UpdateReportScheduleCommand : ReportScheduleCommandBase
{
    public required int ExpectedVersion { get; init; }
}

/// <summary>C4 -- pause a schedule (no run, no catch-up).</summary>
public sealed class PauseReportScheduleCommand : Command
{
    public required Guid ScheduleId { get; init; }
}

/// <summary>C4 -- resume a paused schedule; the next run is computed from now.</summary>
public sealed class ResumeReportScheduleCommand : Command
{
    public required Guid ScheduleId { get; init; }
}

/// <summary>C4 -- cancel a schedule (terminal; kept for history).</summary>
public sealed class CancelReportScheduleCommand : Command
{
    public required Guid ScheduleId { get; init; }
}

/// <summary>
/// C5 -- a generated run. One aggregate: the run. The file is NOT on this command: the runner has already stored it through HO-FIL-001 (<c>IFileStore</c> and <c>RegisterStoredFileCommand</c>, as the
/// upload endpoint does) as its own step and passes the resulting <see cref="FileId"/> (null when the export was too large to attach, <see cref="AttachmentOmitted"/>). The handler creates the run, enlists
/// one email per recipient (the file attached by reference, the run id) and saves them together (HO-NTF-001 F23). A duplicate slot is success with nothing written; the runner then removes its file.
/// </summary>
public sealed class RecordScheduledReportRunCommand : Command
{
    public required Guid RunId { get; init; }
    public required Guid ScheduleId { get; init; }
    public required DateTimeOffset DueOn { get; init; }
    public required ReportRunTrigger Trigger { get; init; }
    public required DateOnly PeriodFrom { get; init; }
    public required DateOnly PeriodTo { get; init; }
    public required string TimeZoneId { get; init; }
    public required ReportFormat Format { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long ContentLength { get; init; }
    public required string ContentSha256 { get; init; }

    /// <summary>The HO-FIL-001 stored file; null exactly when <see cref="AttachmentOmitted"/>.</summary>
    public Guid? FileId { get; init; }

    public bool AttachmentOmitted { get; init; }
    public required int RowCount { get; init; }
    public required IReadOnlyList<string> ColumnKeys { get; init; }
}

/// <summary>C6 -- a failed run: a <c>Failed</c> row with a code, no file and no email.</summary>
public sealed class RecordFailedReportRunCommand : Command
{
    public required Guid RunId { get; init; }
    public required Guid ScheduleId { get; init; }
    public required DateTimeOffset DueOn { get; init; }
    public required ReportRunTrigger Trigger { get; init; }
    public required string FailureCode { get; init; }
    public DateOnly? PeriodFrom { get; init; }
    public DateOnly? PeriodTo { get; init; }
    public string? TimeZoneId { get; init; }
}

/// <summary>C7 -- the slot is done: record it and compute the next run (idempotent; missed slots collapse to one).</summary>
public sealed class AdvanceReportScheduleCommand : Command
{
    public required Guid ScheduleId { get; init; }
    public required DateTimeOffset DueOn { get; init; }
}

/// <summary>The result of C2 and C3: when the schedule next runs.</summary>
public sealed record ReportScheduleResult(Guid ScheduleId, DateTimeOffset? NextRunOn);

/// <summary>The result of C5: the run, and whether this call created it (false = the slot had already run: success, nothing written).</summary>
public sealed record RecordedRunResult(Guid RunId, string Status, int RowCount, bool Created);
