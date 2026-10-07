namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-APT-001 §8.10/§10.1 -- why a future booked appointment was flagged for rescheduling
/// (ADR-076). The flag is informational only; nothing here cancels or messages automatically.</summary>
public sealed class RescheduleFlagReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RescheduleFlagReason PerformerOnLeave = new(1, nameof(PerformerOnLeave));
    public static readonly RescheduleFlagReason BranchClosed = new(2, nameof(BranchClosed));
    public static readonly RescheduleFlagReason CatDeceased = new(3, nameof(CatDeceased));
    public static readonly RescheduleFlagReason CatOwnerChanged = new(4, nameof(CatOwnerChanged));
}
