namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1 -- what a treatment line records. <c>Medicine</c> and <c>Consumable</c> move stock; <c>Procedure</c> does not.</summary>
public sealed class TreatmentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly TreatmentKind Procedure = new(1, nameof(Procedure));
    public static readonly TreatmentKind Medicine = new(2, nameof(Medicine));
    public static readonly TreatmentKind Consumable = new(3, nameof(Consumable));

    public bool MovesStock => this == Medicine || this == Consumable;
}
