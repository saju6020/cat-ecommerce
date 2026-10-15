namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class FollowUpKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly FollowUpKind Appointment = new(1, nameof(Appointment));
    public static readonly FollowUpKind Reminder = new(2, nameof(Reminder));
}
