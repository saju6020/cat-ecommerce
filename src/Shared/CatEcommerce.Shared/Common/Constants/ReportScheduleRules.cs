namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 10.3 / 8.9 (ADR-287, ADR-288) -- the fixed numbers of scheduled reports.</summary>
public static class ReportScheduleRules
{
    public const int NameMaxLength = 100;
    public const int MaxRecipients = 20;

    /// <summary>A slot is tried this many times; the third failed run advances the schedule anyway, so one bad slot never blocks it.</summary>
    public const int MaxAttemptsPerSlot = 3;

    public const int SendAtStepMinutes = 5;
    public const int MaxSchedulesPerPass = 20;
    public const int MaxDayOfMonth = 28;
    public const int PreviewRuns = 3;
    public const int FailureCodeMaxLength = 60;

    /// <summary>The attachment file name: lower-case letters, digits and hyphens, then <c>.csv</c> or <c>.pdf</c>.</summary>
    public const string FileNamePattern = @"^[a-z0-9-]+\.(csv|pdf)$";
}

/// <summary>HO-RPT-001 -- the worker's identity on a scheduled run (Reporting adds its own constant; no other area's file is edited).</summary>
public static class ReportingSystemActors
{
    public static readonly Guid ReportingWorker = new("00000000-0000-0000-0000-000000000ff6");
}
