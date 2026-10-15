namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class ConsentStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ConsentStatus Recorded = new(1, nameof(Recorded));
    public static readonly ConsentStatus Withdrawn = new(2, nameof(Withdrawn));
}
