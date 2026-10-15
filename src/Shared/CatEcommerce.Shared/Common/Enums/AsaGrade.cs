namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1 -- ASA physical status I to V, ordered by <c>Value</c>. The "E" (emergency) suffix is a separate flag on the surgery.</summary>
public sealed class AsaGrade(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AsaGrade I = new(1, nameof(I));
    public static readonly AsaGrade II = new(2, nameof(II));
    public static readonly AsaGrade III = new(3, nameof(III));
    public static readonly AsaGrade IV = new(4, nameof(IV));
    public static readonly AsaGrade V = new(5, nameof(V));
}
