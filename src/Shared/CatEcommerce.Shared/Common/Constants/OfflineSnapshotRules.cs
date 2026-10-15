namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-XCT-001 §10.3 / ADR-306.</summary>
public static class OfflineSnapshotRules
{
    public const int ValidityGraceHours = 2;
    public const int SchemaVersion = 1;
    public const string AppointmentsSection = "appointments";
    public const string BoardingTasksSection = "boardingTasks";
}
