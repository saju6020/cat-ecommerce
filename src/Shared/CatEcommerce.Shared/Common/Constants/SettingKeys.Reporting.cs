namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Reporting's (HO-RPT-001 section 10.2) own registry keys, per ADR-341's per-area partial pattern.</summary>
public static partial class SettingKeys
{
    public const string ReportingExportRetentionDays = "reporting.exportRetentionDays";
    public const string ReportingMaxAttachmentMegabytes = "reporting.maxAttachmentMegabytes";
    public const string ReportingDashboardRefreshSeconds = "reporting.dashboardRefreshSeconds";
}
