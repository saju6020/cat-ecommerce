using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>The nine settings HO-XCT-001 appends to the registry (§10.2, ADR-301). Periods are settings with the SRS's
/// assumed defaults (SRS Q-1 is open): a wrong default can delay an anonymisation or a review flag, never destroy a record.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> OperationsEntries =
    [
        new SettingDefinition(SettingKeys.RetentionClinicalYears, "Retention", SettingType.Integer, "1", "50", "years", "7", false,
            "Clinical record retention", "Years after a cat's last visit or death that its clinical record is reported as eligible for review (D-8). Nothing is deleted automatically."),
        new SettingDefinition(SettingKeys.RetentionFinancialYears, "Retention", SettingType.Integer, "1", "50", "years", "6", false,
            "Financial record retention", "Years after the document date that a financial record is reported as eligible for review, and that personal data held inside it is erased (D-9)."),
        new SettingDefinition(SettingKeys.RetentionBoardingMediaMonths, "Retention", SettingType.Integer, "1", "120", "months", "12", false,
            "Boarding photo and video retention", "Months after which boarding photos and videos not attached to an incident are purged (D-10)."),
        new SettingDefinition(SettingKeys.RetentionNotificationContentMonths, "Retention", SettingType.Integer, "1", "120", "months", "24", false,
            "Notification content retention", "Months after which the text of a sent notification is redacted. Channel, template, status and timestamps are kept."),
        new SettingDefinition(SettingKeys.RetentionSecurityArtefactDays, "Retention", SettingType.Integer, "7", "365", "days", "30", false,
            "Session and token retention", "Days after expiry or end that sign-in sessions and password-reset tokens are purged."),
        new SettingDefinition(SettingKeys.RetentionWebVitalsDays, "Retention", SettingType.Integer, "7", "730", "days", "90", false,
            "Performance telemetry retention", "Days that anonymous page-performance samples are kept."),
        new SettingDefinition(SettingKeys.RetentionSweepHour, "Retention", SettingType.Integer, "0", "23", "hour", "2", false,
            "Nightly retention hour", "The hour of day (main branch local time) the nightly retention passes start."),
        new SettingDefinition(SettingKeys.OperationsRestoreTestIntervalDays, "Operations", SettingType.Integer, "7", "92", "days", "92", false,
            "Restore test interval", "Days after the last passed restore test that the next one is due (quarterly is the ceiling, NFR-AVL-5)."),
        new SettingDefinition(SettingKeys.OfflineSnapshotRefreshSeconds, "Operations", SettingType.Integer, "30", "600", "seconds", "60", true,
            "Offline snapshot refresh", "How often a front-desk terminal asks for a fresh offline snapshot."),
    ];
}
