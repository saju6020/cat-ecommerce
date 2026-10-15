namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- Whether a medicine is dosed per kilogram of body weight or a fixed dose. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DosingBasis(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DosingBasis PerKg = new(1, nameof(PerKg));
    public static readonly DosingBasis Fixed = new(2, nameof(Fixed));
}
