namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1. Decides a report's access action (section 8.8).</summary>
public sealed class ReportCategory(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportCategory ClinicOperations = new(1, nameof(ClinicOperations));
    public static readonly ReportCategory Clinical = new(2, nameof(Clinical));
    public static readonly ReportCategory Boarding = new(3, nameof(Boarding));
    public static readonly ReportCategory Stock = new(4, nameof(Stock));
    public static readonly ReportCategory Sales = new(5, nameof(Sales));
    public static readonly ReportCategory Financial = new(6, nameof(Financial));
    public static readonly ReportCategory Growth = new(7, nameof(Growth));
}
