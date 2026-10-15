using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 10.2 -- Reporting's three settings (a new group "Reporting"); none has a branch override. Values are rows, so no migration.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> ReportingEntries =
    [
        new SettingDefinition(
            SettingKeys.ReportingExportRetentionDays, "Reporting", SettingType.Integer, "1", "90", "days", "7",
            false, "Report file retention", "Days a generated scheduled-report file is kept before the retention worker may remove it."),

        new SettingDefinition(
            SettingKeys.ReportingMaxAttachmentMegabytes, "Reporting", SettingType.Integer, "1", "20", "MB", "10",
            false, "Largest report attachment", "A scheduled report larger than this is not attached to the email; the email carries a link instead."),

        new SettingDefinition(
            SettingKeys.ReportingDashboardRefreshSeconds, "Reporting", SettingType.Integer, "30", "600", "seconds", "60",
            false, "Dashboard refresh interval", "How often the home dashboard asks for fresh figures."),
    ];
}
