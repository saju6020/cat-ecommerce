namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-BIL-001 Phase J -- what one billable line of a visit is, in EMR's own vocabulary: the three <see cref="TreatmentKind"/>s plus a diagnostic
/// test. Billing maps these to its own <see cref="ItemKind"/> (<c>VisitBillableLineKindMap</c>); EMR never speaks Billing's kinds.
/// </summary>
public sealed class VisitBillableLineKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly VisitBillableLineKind Procedure = new(1, nameof(Procedure));
    public static readonly VisitBillableLineKind Medicine = new(2, nameof(Medicine));
    public static readonly VisitBillableLineKind Consumable = new(3, nameof(Consumable));
    public static readonly VisitBillableLineKind Diagnostic = new(4, nameof(Diagnostic));

    /// <summary>EMR's translation of a treatment's kind. Exhaustive: a new <see cref="TreatmentKind"/> without a case throws, so it cannot be billed by accident.</summary>
    public static VisitBillableLineKind Of(TreatmentKind kind)
    {
        if (kind == TreatmentKind.Procedure) return Procedure;
        if (kind == TreatmentKind.Medicine) return Medicine;
        if (kind == TreatmentKind.Consumable) return Consumable;
        throw new ArgumentOutOfRangeException(nameof(kind), kind.DisplayName, "This treatment kind has no billable line kind.");
    }
}
