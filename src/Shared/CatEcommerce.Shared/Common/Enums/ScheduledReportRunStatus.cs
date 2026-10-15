namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1.</summary>
public sealed class ScheduledReportRunStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ScheduledReportRunStatus Generated = new(1, nameof(Generated));
    public static readonly ScheduledReportRunStatus Failed = new(2, nameof(Failed));
    public static readonly ScheduledReportRunStatus Purged = new(3, nameof(Purged));
}
