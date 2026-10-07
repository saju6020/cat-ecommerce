namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §8.6/§10.1 -- the notification's delivery state machine.</summary>
public sealed class NotificationStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationStatus Queued = new(1, nameof(Queued));
    public static readonly NotificationStatus Sending = new(2, nameof(Sending));
    public static readonly NotificationStatus Sent = new(3, nameof(Sent));
    public static readonly NotificationStatus RetryPending = new(4, nameof(RetryPending));
    public static readonly NotificationStatus Undeliverable = new(5, nameof(Undeliverable));
    public static readonly NotificationStatus Expired = new(6, nameof(Expired));
    public static readonly NotificationStatus Cancelled = new(7, nameof(Cancelled));
    public static readonly NotificationStatus InboxDelivered = new(8, nameof(InboxDelivered));
}
