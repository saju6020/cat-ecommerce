namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- how a staff-taken erasure request reached the clinic.</summary>
public sealed class ErasureRequestChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ErasureRequestChannel InPerson = new(1, nameof(InPerson));
    public static readonly ErasureRequestChannel Phone = new(2, nameof(Phone));
    public static readonly ErasureRequestChannel Email = new(3, nameof(Email));
    public static readonly ErasureRequestChannel Letter = new(4, nameof(Letter));
}
