namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.11 / §10.1.</summary>
public sealed class LoyaltyEntryKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly LoyaltyEntryKind Accrual = new(1, nameof(Accrual));
    public static readonly LoyaltyEntryKind AccrualReversal = new(2, nameof(AccrualReversal));
    public static readonly LoyaltyEntryKind Redemption = new(3, nameof(Redemption));
    public static readonly LoyaltyEntryKind RedemptionReversal = new(4, nameof(RedemptionReversal));
    public static readonly LoyaltyEntryKind MergeTransferIn = new(5, nameof(MergeTransferIn));
    public static readonly LoyaltyEntryKind MergeTransferOut = new(6, nameof(MergeTransferOut));
}
