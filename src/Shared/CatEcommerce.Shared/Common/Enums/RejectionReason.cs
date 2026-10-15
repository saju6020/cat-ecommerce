namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- Why a received quantity was rejected (§8.6). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class RejectionReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RejectionReason Damaged = new(1, nameof(Damaged));
    public static readonly RejectionReason Expired = new(2, nameof(Expired));
    public static readonly RejectionReason ShortShelfLife = new(3, nameof(ShortShelfLife));
    public static readonly RejectionReason WrongItem = new(4, nameof(WrongItem));
    public static readonly RejectionReason TemperatureBreach = new(5, nameof(TemperatureBreach));
    public static readonly RejectionReason FailedInspection = new(6, nameof(FailedInspection));
    public static readonly RejectionReason Other = new(7, nameof(Other));
}
