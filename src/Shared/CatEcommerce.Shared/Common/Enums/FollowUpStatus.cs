namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class FollowUpStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly FollowUpStatus Pending = new(1, nameof(Pending));
    public static readonly FollowUpStatus Done = new(2, nameof(Done));
    public static readonly FollowUpStatus Cancelled = new(3, nameof(Cancelled));
}
