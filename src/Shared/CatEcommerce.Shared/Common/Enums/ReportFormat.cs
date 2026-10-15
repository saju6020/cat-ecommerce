namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1.</summary>
public sealed class ReportFormat(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportFormat Csv = new(1, nameof(Csv));
    public static readonly ReportFormat Pdf = new(2, nameof(Pdf));
}
