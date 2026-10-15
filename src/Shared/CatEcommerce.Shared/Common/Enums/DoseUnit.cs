namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The unit of a prescribed dose. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DoseUnit(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DoseUnit Mg = new(1, nameof(Mg));
    public static readonly DoseUnit Mcg = new(2, nameof(Mcg));
    public static readonly DoseUnit G = new(3, nameof(G));
    public static readonly DoseUnit Ml = new(4, nameof(Ml));
    public static readonly DoseUnit Tablet = new(5, nameof(Tablet));
    public static readonly DoseUnit Capsule = new(6, nameof(Capsule));
    public static readonly DoseUnit Drop = new(7, nameof(Drop));
    public static readonly DoseUnit Application = new(8, nameof(Application));
    public static readonly DoseUnit Iu = new(9, nameof(Iu));
}
