using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Reporting;

/// <summary>
/// HO-RPT-001 section 8.9 / 8.10 (ADR-287, ADR-289) -- one run of a schedule: generated, failed, or later purged. The file itself is in HO-FIL-001 and is referenced by <see cref="FileId"/>; no file
/// bytes are ever held here (so none reach the audit trail -- U-G8). A unique index on (schedule, due time) for scheduled runs that were generated or purged is what makes a slot run exactly once.
/// </summary>
public sealed class ScheduledReportRunAggregate : AggregateRoot
{
    private readonly List<string> _columnsIncluded = [];

    public Guid ScheduleId { get; private set; }
    public DateTimeOffset DueOn { get; private set; }
    public ReportRunTrigger Trigger { get; private set; } = default!;
    public ScheduledReportRunStatus Status { get; private set; } = default!;
    public DateOnly PeriodFrom { get; private set; }
    public DateOnly PeriodTo { get; private set; }
    public string TimeZoneId { get; private set; } = default!;
    public ReportFormat Format { get; private set; } = default!;
    public string? FileName { get; private set; }
    public string? ContentType { get; private set; }
    public long ContentLength { get; private set; }
    public string? ContentSha256 { get; private set; }
    public int RowCount { get; private set; }
    public IReadOnlyList<string> ColumnsIncluded => _columnsIncluded;
    public bool AttachmentOmitted { get; private set; }

    /// <summary>The HO-FIL-001 stored file holding the export; null when the file was too large to attach, or the run failed.</summary>
    public Guid? FileId { get; private set; }

    public string? FailureCode { get; private set; }
    public DateTimeOffset GeneratedOn { get; private set; }
    public DateTimeOffset? PurgedOn { get; private set; }

    private ScheduledReportRunAggregate() { }

    public ScheduledReportRunAggregate(Guid id) : base(id) { }

    public void RecordGenerated(ScheduledReportRunDto dto)
    {
        Fill(dto);
        Status = ScheduledReportRunStatus.Generated;
        FileName = dto.FileName;
        ContentType = dto.ContentType;
        ContentLength = dto.ContentLength;
        ContentSha256 = dto.ContentSha256;
        RowCount = dto.RowCount;
        AttachmentOmitted = dto.AttachmentOmitted;
        FileId = dto.AttachmentOmitted ? null : dto.FileId;
        _columnsIncluded.Clear();
        _columnsIncluded.AddRange(dto.ColumnsIncluded);

        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    public void RecordFailed(ScheduledReportRunDto dto, string failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > ReportScheduleRules.FailureCodeMaxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReportSchedule, "A failed run needs a short failure code.", nameof(failureCode));

        Fill(dto);
        Status = ScheduledReportRunStatus.Failed;
        FailureCode = failureCode;
        FileId = null;

        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    /// <summary>Idempotent. The file is gone; a notification not yet sent then fails its guard and is cancelled.</summary>
    public void Purge(DateTimeOffset now, UserContext userContext)
    {
        if (Status != ScheduledReportRunStatus.Generated) return;

        Status = ScheduledReportRunStatus.Purged;
        PurgedOn = now;
        FileId = null;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    private void Fill(ScheduledReportRunDto dto)
    {
        ScheduleId = dto.ScheduleId;
        DueOn = dto.DueOn;
        Trigger = dto.Trigger;
        PeriodFrom = dto.PeriodFrom;
        PeriodTo = dto.PeriodTo;
        TimeZoneId = dto.TimeZoneId;
        Format = dto.Format;
        GeneratedOn = dto.Now;
    }
}
