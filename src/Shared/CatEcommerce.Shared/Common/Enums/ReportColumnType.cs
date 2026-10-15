namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1.</summary>
public sealed class ReportColumnType(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportColumnType Text = new(1, nameof(Text));
    public static readonly ReportColumnType Integer = new(2, nameof(Integer));
    public static readonly ReportColumnType Decimal = new(3, nameof(Decimal));
    public static readonly ReportColumnType Money = new(4, nameof(Money));
    public static readonly ReportColumnType Percent = new(5, nameof(Percent));
    public static readonly ReportColumnType Date = new(6, nameof(Date));
    public static readonly ReportColumnType Instant = new(7, nameof(Instant));
    public static readonly ReportColumnType Boolean = new(8, nameof(Boolean));
}
