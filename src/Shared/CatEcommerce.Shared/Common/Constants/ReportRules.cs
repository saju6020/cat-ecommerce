using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 10.3 -- limits shared by the framework, the exports and the access policy.</summary>
public static class ReportRules
{
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 50;
    public const int CsvMaxRows = 50_000;
    public const int PdfMaxRows = 5_000;
    public const int DefaultMaxRangeDays = 366;
    public const int KeyMaxLength = 60;

    /// <summary>The roles that may ask for <c>branchId=all</c> (OQ-6).</summary>
    public static readonly IReadOnlyList<string> CrossBranchRoles = [StaffRoles.Administrator, StaffRoles.Accountant];

    public const string AllBranches = "all";
    public const string AllBranchesName = "All branches";

    /// <summary>The access action that opens a report of <paramref name="category"/> (section 8.8).</summary>
    public static string ActionFor(ReportCategory category)
    {
        if (category == ReportCategory.ClinicOperations) return Actions.Reports.ClinicOperationsView;
        if (category == ReportCategory.Clinical) return Actions.Reports.ClinicalView;
        if (category == ReportCategory.Boarding) return Actions.Reports.BoardingView;
        if (category == ReportCategory.Stock) return Actions.Reports.StockView;
        if (category == ReportCategory.Sales) return Actions.Reports.SalesView;
        if (category == ReportCategory.Financial) return Actions.Reports.FinancialView;
        return Actions.Reports.GrowthView;
    }

    /// <summary>The pattern of a report key: lowercase words joined by hyphens, e.g. <c>clinic-daily-schedule</c>.</summary>
    public static readonly Regex KeyPattern = new("^[a-z]+(-[a-z0-9]+)+$", RegexOptions.Compiled);

    /// <summary>The audit entry written for every export (never row data).</summary>
    public const string ExportAuditAction = "ReportExported";
    public const string ExportAuditEntityType = "Report";
}

/// <summary>
/// HO-RPT-001 section 8.3 R6 / section 10.1 -- the expiry bands of the near-expiry report, <b>pure</b>. A batch is in the tightest band it has crossed against the three thresholds HO-IMP-001 configures
/// (default 90 / 60 / 30 days); a batch past its expiry is <c>Expired</c>; one beyond the loosest threshold is in no band.
/// </summary>
public static class ExpiryBandRules
{
    public static (ExpiryBand? Band, int? Threshold) BandFor(int daysToExpiry, IReadOnlyList<int> thresholds)
    {
        if (daysToExpiry < 0) return (ExpiryBand.Expired, null);

        var tightest = Helpers.ExpiryAlertRules.TightestCrossed(daysToExpiry, thresholds);
        if (tightest is not { } threshold) return (null, null);

        var ascending = thresholds.OrderBy(t => t).ToList();
        var index = ascending.IndexOf(threshold);
        var band = index <= 0 ? ExpiryBand.Within30Days : index == 1 ? ExpiryBand.Within60Days : ExpiryBand.Within90Days;
        return (band, threshold);
    }

    /// <summary>The printed band: "Expired", or "Within N days" with the threshold actually configured.</summary>
    public static string? Label(ExpiryBand? band, int? threshold) =>
        band is null ? null : band == ExpiryBand.Expired ? "Expired" : $"Within {threshold} days";
}

/// <summary>HO-RPT-001 OQ-18 -- the order fulfilment report's service level. HO-ORD-001 defines no dispatch SLA setting, so the handover's default applies: 24 hours from confirmation to dispatch.</summary>
public static class FulfilmentRules
{
    public const int DispatchSlaHours = 24;
}
