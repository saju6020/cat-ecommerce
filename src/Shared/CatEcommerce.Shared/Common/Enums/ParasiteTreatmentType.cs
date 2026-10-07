namespace CatEcommerce.Shared.Common.Enums;

public sealed class ParasiteTreatmentType(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ParasiteTreatmentType Deworming = new(1, nameof(Deworming));
    public static readonly ParasiteTreatmentType FleaAndTick = new(2, nameof(FleaAndTick));
    public static readonly ParasiteTreatmentType Combined = new(3, nameof(Combined));
}
