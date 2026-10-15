namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1.</summary>
public sealed class ReportScheduleStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportScheduleStatus Active = new(1, nameof(Active));
    public static readonly ReportScheduleStatus Paused = new(2, nameof(Paused));
    public static readonly ReportScheduleStatus Cancelled = new(3, nameof(Cancelled));
}
