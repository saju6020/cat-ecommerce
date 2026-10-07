namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §10.1. <see cref="Urgent"/> ignores quiet hours (§8.14, ADR-059).</summary>
public sealed class NotificationUrgency(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationUrgency Normal = new(1, nameof(Normal));
    public static readonly NotificationUrgency Urgent = new(2, nameof(Urgent));
}
