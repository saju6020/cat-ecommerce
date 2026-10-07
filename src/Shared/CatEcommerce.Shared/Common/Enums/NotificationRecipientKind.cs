namespace CatEcommerce.Shared.Common.Enums;

/// <summary>Owned by HO-NTF-001 §10.1.</summary>
public sealed class NotificationRecipientKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationRecipientKind Customer = new(1, nameof(Customer));
    public static readonly NotificationRecipientKind Staff = new(2, nameof(Staff));
}
