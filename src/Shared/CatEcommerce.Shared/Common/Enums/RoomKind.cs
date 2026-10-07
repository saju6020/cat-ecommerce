namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1.</summary>
public sealed class RoomKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RoomKind Consultation = new(1, nameof(Consultation));
    public static readonly RoomKind Treatment = new(2, nameof(Treatment));
    public static readonly RoomKind Theatre = new(3, nameof(Theatre));
    public static readonly RoomKind Grooming = new(4, nameof(Grooming));
}
