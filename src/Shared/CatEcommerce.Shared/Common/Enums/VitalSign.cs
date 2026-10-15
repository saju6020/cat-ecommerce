namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class VitalSign(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly VitalSign Weight = new(1, nameof(Weight));
    public static readonly VitalSign Temperature = new(2, nameof(Temperature));
    public static readonly VitalSign HeartRate = new(3, nameof(HeartRate));
    public static readonly VitalSign RespiratoryRate = new(4, nameof(RespiratoryRate));
    public static readonly VitalSign BodyConditionScore = new(5, nameof(BodyConditionScore));
    public static readonly VitalSign PainScore = new(6, nameof(PainScore));
}
