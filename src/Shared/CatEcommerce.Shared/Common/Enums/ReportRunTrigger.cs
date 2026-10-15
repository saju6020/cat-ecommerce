namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1.</summary>
public sealed class ReportRunTrigger(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportRunTrigger Schedule = new(1, nameof(Schedule));
    public static readonly ReportRunTrigger Manual = new(2, nameof(Manual));
}
