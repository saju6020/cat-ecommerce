namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The dosage form of a formulary medicine. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class MedicineForm(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly MedicineForm Tablet = new(1, nameof(Tablet));
    public static readonly MedicineForm Capsule = new(2, nameof(Capsule));
    public static readonly MedicineForm OralLiquid = new(3, nameof(OralLiquid));
    public static readonly MedicineForm OralPaste = new(4, nameof(OralPaste));
    public static readonly MedicineForm Powder = new(5, nameof(Powder));
    public static readonly MedicineForm Injection = new(6, nameof(Injection));
    public static readonly MedicineForm Topical = new(7, nameof(Topical));
    public static readonly MedicineForm EyeDrops = new(8, nameof(EyeDrops));
    public static readonly MedicineForm EarDrops = new(9, nameof(EarDrops));
    public static readonly MedicineForm Transdermal = new(10, nameof(Transdermal));
    public static readonly MedicineForm Other = new(11, nameof(Other));
}
