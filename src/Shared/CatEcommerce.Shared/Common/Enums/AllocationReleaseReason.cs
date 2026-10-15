namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.8 / §10.1. Staff cannot 'move' a payment -- only these two reasons release an allocation.</summary>
public sealed class AllocationReleaseReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AllocationReleaseReason CreditNoteExcess = new(1, nameof(CreditNoteExcess));
    public static readonly AllocationReleaseReason PaymentReversed = new(2, nameof(PaymentReversed));
}
