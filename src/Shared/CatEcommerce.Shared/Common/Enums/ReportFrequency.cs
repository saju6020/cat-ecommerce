namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1.</summary>
public sealed class ReportFrequency(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportFrequency Daily = new(1, nameof(Daily));
    public static readonly ReportFrequency Weekly = new(2, nameof(Weekly));
    public static readonly ReportFrequency Monthly = new(3, nameof(Monthly));
}
