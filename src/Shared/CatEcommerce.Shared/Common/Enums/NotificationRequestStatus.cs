namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §8.4/§10.1.</summary>
public sealed class NotificationRequestStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationRequestStatus Pending = new(1, nameof(Pending));
    public static readonly NotificationRequestStatus Expanded = new(2, nameof(Expanded));
    public static readonly NotificationRequestStatus Suppressed = new(3, nameof(Suppressed));
    public static readonly NotificationRequestStatus Expired = new(4, nameof(Expired));
}
