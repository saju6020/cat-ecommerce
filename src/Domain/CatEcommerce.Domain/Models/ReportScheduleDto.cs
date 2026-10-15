using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-RPT-001 section 11.3 -- a schedule's editable fields. <paramref name="Definition"/> and <paramref name="Zone"/> are resolved by the handler (the catalogue, and the branch's -- or the main
/// branch's -- time zone) so the aggregate can check the period and compute the next run without reading anything itself.
/// </summary>
public sealed record ReportScheduleDto(
    string Name,
    ReportDefinition Definition,
    Guid? BranchId,
    ReportRelativePeriod Period,
    IReadOnlyDictionary<string, string> Parameters,
    ReportFormat Format,
    ReportFrequency Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    bool LastDayOfMonth,
    TimeOnly SendAtLocal,
    IReadOnlyList<Guid> RecipientIds,
    TimeZoneInfo Zone,
    DateTimeOffset Now,
    UserContext UserContext);

/// <summary>HO-RPT-001 section 11.3 -- a run of a schedule, generated or failed. No file bytes: the file lives in HO-FIL-001 and is referenced by <paramref name="FileId"/>.</summary>
public sealed record ScheduledReportRunDto(
    Guid ScheduleId,
    DateTimeOffset DueOn,
    ReportRunTrigger Trigger,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string TimeZoneId,
    ReportFormat Format,
    string? FileName,
    string? ContentType,
    long ContentLength,
    string? ContentSha256,
    int RowCount,
    IReadOnlyList<string> ColumnsIncluded,
    bool AttachmentOmitted,
    Guid? FileId,
    DateTimeOffset Now,
    UserContext UserContext);
