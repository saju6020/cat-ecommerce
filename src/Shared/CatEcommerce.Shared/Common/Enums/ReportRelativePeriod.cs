namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1, plus Next30Days and Next90Days for the forward windows of the vaccination due list and the near-expiry report (HO-RPT-001 section 1 Amended, 2026-10-13).</summary>
public sealed class ReportRelativePeriod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReportRelativePeriod Today = new(1, nameof(Today));
    public static readonly ReportRelativePeriod PreviousDay = new(2, nameof(PreviousDay));
    public static readonly ReportRelativePeriod PreviousWeek = new(3, nameof(PreviousWeek));
    public static readonly ReportRelativePeriod PreviousMonth = new(4, nameof(PreviousMonth));
    public static readonly ReportRelativePeriod MonthToDate = new(5, nameof(MonthToDate));
    public static readonly ReportRelativePeriod Last7Days = new(6, nameof(Last7Days));
    public static readonly ReportRelativePeriod Last30Days = new(7, nameof(Last30Days));
    public static readonly ReportRelativePeriod Next30Days = new(8, nameof(Next30Days));
    public static readonly ReportRelativePeriod Next90Days = new(9, nameof(Next90Days));
}
