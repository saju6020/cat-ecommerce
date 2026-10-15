namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The unit of a formulary medicine strength. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StrengthUnit(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StrengthUnit Mg = new(1, nameof(Mg));
    public static readonly StrengthUnit Mcg = new(2, nameof(Mcg));
    public static readonly StrengthUnit G = new(3, nameof(G));
    public static readonly StrengthUnit Iu = new(4, nameof(Iu));
    public static readonly StrengthUnit Percent = new(5, nameof(Percent));
}
