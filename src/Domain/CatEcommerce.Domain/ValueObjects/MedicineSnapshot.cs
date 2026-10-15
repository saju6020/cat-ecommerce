using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-PHM-001 §8.3 (ADR-138) -- the formulary medicine frozen at issue: a later formulary edit never changes a prescription.</summary>
public sealed record MedicineSnapshot
{
    public string Code { get; init; }
    public string Name { get; init; }
    public MedicineForm Form { get; init; }
    public string StrengthText { get; init; }
    public bool IsControlled { get; init; }
    public string? ControlledSchedule { get; init; }
    public DispensingUnit DispensingUnit { get; init; }
    public Guid[] LinkedSkuIds { get; init; }

    public MedicineSnapshot(
        string code, string name, MedicineForm form, string strengthText, bool isControlled, string? controlledSchedule,
        DispensingUnit dispensingUnit, IEnumerable<Guid> linkedSkuIds)
    {
        Code = code;
        Name = name;
        Form = form;
        StrengthText = strengthText;
        IsControlled = isControlled;
        ControlledSchedule = controlledSchedule;
        DispensingUnit = dispensingUnit;
        LinkedSkuIds = linkedSkuIds.ToArray();
    }

    // EF only.
    private MedicineSnapshot()
    {
        Code = default!;
        Name = default!;
        Form = default!;
        StrengthText = default!;
        DispensingUnit = default!;
        LinkedSkuIds = [];
    }
}
