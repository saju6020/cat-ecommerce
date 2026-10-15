namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1.</summary>
public sealed class RetentionRunStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RetentionRunStatus Requested = new(1, nameof(Requested));
    public static readonly RetentionRunStatus Running = new(2, nameof(Running));
    public static readonly RetentionRunStatus Succeeded = new(3, nameof(Succeeded));
    public static readonly RetentionRunStatus CompletedWithErrors = new(4, nameof(CompletedWithErrors));
}
