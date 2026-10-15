namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class SurgeryPricingBasis(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SurgeryPricingBasis Standard = new(1, nameof(Standard));
    public static readonly SurgeryPricingBasis Campaign = new(2, nameof(Campaign));
}
