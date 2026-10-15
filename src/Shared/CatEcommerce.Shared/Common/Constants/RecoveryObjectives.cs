namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-XCT-001 §10.3 / NFR-AVL-4: RPO 15 minutes, RTO 4 hours. Measured by the restore drill, never asserted (ADR-311, ADR-313).</summary>
public static class RecoveryObjectives
{
    public const int RpoMinutes = 15;
    public const int RtoMinutes = 240;
    public const int BackupStaleHours = 26;
    public const int ArchiveStaleMinutes = 15;
}
