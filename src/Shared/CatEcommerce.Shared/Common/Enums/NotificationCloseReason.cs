namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §8.6/§10.1.</summary>
public sealed class NotificationCloseReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationCloseReason SubjectNoLongerApplies = new(1, nameof(SubjectNoLongerApplies));
    public static readonly NotificationCloseReason RecipientInactive = new(2, nameof(RecipientInactive));
    public static readonly NotificationCloseReason RecipientOptedOut = new(3, nameof(RecipientOptedOut));
    public static readonly NotificationCloseReason NotAfterPassed = new(4, nameof(NotAfterPassed));
}
