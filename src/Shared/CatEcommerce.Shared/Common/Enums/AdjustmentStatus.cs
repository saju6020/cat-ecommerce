namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.10. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class AdjustmentStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AdjustmentStatus Posted = new(1, nameof(Posted));
    public static readonly AdjustmentStatus PendingApproval = new(2, nameof(PendingApproval));
    public static readonly AdjustmentStatus Rejected = new(3, nameof(Rejected));
    public static readonly AdjustmentStatus Withdrawn = new(4, nameof(Withdrawn));
}
