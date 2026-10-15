namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Cross-cutting / Operations (HO-XCT-001 §10.2) registry keys.</summary>
public static partial class SettingKeys
{
    public const string RetentionClinicalYears = "retention.clinicalYears";
    public const string RetentionFinancialYears = "retention.financialYears";
    public const string RetentionBoardingMediaMonths = "retention.boardingMediaMonths";
    public const string RetentionNotificationContentMonths = "retention.notificationContentMonths";
    public const string RetentionSecurityArtefactDays = "retention.securityArtefactDays";
    public const string RetentionWebVitalsDays = "retention.webVitalsDays";
    public const string RetentionSweepHour = "retention.sweepHour";
    public const string OperationsRestoreTestIntervalDays = "operations.restoreTestIntervalDays";
    public const string OfflineSnapshotRefreshSeconds = "offline.snapshotRefreshSeconds";
}
