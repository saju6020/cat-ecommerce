namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 8.2 -- the parameter types a report definition may declare.</summary>
public sealed class ReportParameterType(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportParameterType Enum = new(1, nameof(Enum));
    public static readonly ReportParameterType Guid = new(2, nameof(Guid));
    public static readonly ReportParameterType Integer = new(3, nameof(Integer));
    public static readonly ReportParameterType Boolean = new(4, nameof(Boolean));
}
