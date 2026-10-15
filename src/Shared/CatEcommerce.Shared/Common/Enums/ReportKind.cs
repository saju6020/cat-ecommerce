namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1. A Period report lists events inside the range; an AsAt report is a position at the end of the range with movements over it.</summary>
public sealed class ReportKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportKind Period = new(1, nameof(Period));
    public static readonly ReportKind AsAt = new(2, nameof(AsAt));
}
