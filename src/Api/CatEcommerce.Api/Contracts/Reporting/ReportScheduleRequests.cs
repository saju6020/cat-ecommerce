namespace CatEcommerce.Api.Contracts.Reporting;

/// <summary>HO-RPT-001 section 12.4 -- the body of create and update. Enumerations travel by name (case-insensitive); <c>sendAtLocal</c> is <c>HH:mm</c>, branch-local. <c>expectedVersion</c> is required on update.</summary>
public sealed class ReportScheduleRequest
{
    public string? Name { get; init; }
    public string? ReportKey { get; init; }

    /// <summary>A branch id, or null for all branches (only for an administrator or an accountant).</summary>
    public Guid? BranchId { get; init; }

    public string? Period { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
    public string? Format { get; init; }
    public string? Frequency { get; init; }
    public string? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public bool LastDayOfMonth { get; init; }
    public string? SendAtLocal { get; init; }
    public List<Guid>? RecipientIds { get; init; }
    public int? ExpectedVersion { get; init; }
}

/// <summary>HO-RPT-001 section 12.4 -- the body of the preview: a draft schedule, nothing is saved.</summary>
public sealed class ReportSchedulePreviewRequest
{
    public string? ReportKey { get; init; }
    public Guid? BranchId { get; init; }
    public string? Period { get; init; }
    public string? Frequency { get; init; }
    public string? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public bool LastDayOfMonth { get; init; }
    public string? SendAtLocal { get; init; }
}
