namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class VisitOrigin(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly VisitOrigin Appointment = new(1, nameof(Appointment));
}
