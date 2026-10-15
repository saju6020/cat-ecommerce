namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.10. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class AdjustmentReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AdjustmentReason Damage = new(1, nameof(Damage));
    public static readonly AdjustmentReason Theft = new(2, nameof(Theft));
    public static readonly AdjustmentReason Sampling = new(3, nameof(Sampling));
    public static readonly AdjustmentReason InternalUse = new(4, nameof(InternalUse));
    public static readonly AdjustmentReason Expired = new(5, nameof(Expired));
    public static readonly AdjustmentReason RecallDisposal = new(6, nameof(RecallDisposal));
    public static readonly AdjustmentReason Found = new(7, nameof(Found));
    public static readonly AdjustmentReason Correction = new(8, nameof(Correction));
}
