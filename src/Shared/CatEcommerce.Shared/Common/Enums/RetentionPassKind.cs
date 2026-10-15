namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- one pass of a retention run (ADR-303).</summary>
public sealed class RetentionPassKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RetentionPassKind Erasure = new(1, nameof(Erasure));
    public static readonly RetentionPassKind PurgeSessions = new(2, nameof(PurgeSessions));
    public static readonly RetentionPassKind PurgeResetTokens = new(3, nameof(PurgeResetTokens));
    public static readonly RetentionPassKind PurgeWebVitals = new(4, nameof(PurgeWebVitals));
    public static readonly RetentionPassKind PurgeBackupRuns = new(5, nameof(PurgeBackupRuns));
    public static readonly RetentionPassKind PurgeSources = new(6, nameof(PurgeSources));
    public static readonly RetentionPassKind RedactNotifications = new(7, nameof(RedactNotifications));
    public static readonly RetentionPassKind ReviewCounts = new(8, nameof(ReviewCounts));
}
