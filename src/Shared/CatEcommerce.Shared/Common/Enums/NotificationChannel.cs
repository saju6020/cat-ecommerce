namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-ACC-001 §10.1. "Reused, never duplicated" by every later handover (HO-NTF-001
/// §10.1). Created here, ahead of Accounts' own build, because HO-NTF-001's <c>NotificationRequestDto</c>
/// (this contracts pass) needs it. <see cref="Push"/> is declared but never a deliverable channel
/// (HO-NTF-001 §4.3, <c>NotificationChannelRules.DeliverableChannels</c> excludes it) -- kept here
/// because it is part of Accounts' own published set, not added by Notifications.
/// </summary>
public sealed class NotificationChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationChannel Email = new(1, nameof(Email));
    public static readonly NotificationChannel Sms = new(2, nameof(Sms));
    public static readonly NotificationChannel Push = new(3, nameof(Push));
    public static readonly NotificationChannel InApp = new(4, nameof(InApp));
}
