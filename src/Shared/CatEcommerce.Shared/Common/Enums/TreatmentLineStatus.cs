namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1. A line is never removed from a visit; a mistake becomes <c>Reversed</c>.</summary>
public sealed class TreatmentLineStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly TreatmentLineStatus Recorded = new(1, nameof(Recorded));
    public static readonly TreatmentLineStatus Reversed = new(2, nameof(Reversed));
}
