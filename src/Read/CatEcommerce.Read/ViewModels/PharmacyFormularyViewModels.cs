using System.Globalization;

namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-PHM-001 §12.4 / UI-8 -- every quantity, dose and weight is returned WITH its unit, the amount as a string:
/// <c>{ "amount": "0.30", "unit": "Ml" }</c>. The client never has to guess a unit.</summary>
public sealed record QuantityViewModel(string Amount, string Unit)
{
    /// <summary>At least two and at most four decimal places, invariant culture (0.3 becomes "0.30", 0.0625 stays "0.0625").</summary>
    public static QuantityViewModel Of(decimal amount, string unit) => new(amount.ToString("0.00##", CultureInfo.InvariantCulture), unit);
}

public sealed record StrengthViewModel(QuantityViewModel Amount, QuantityViewModel Per, string Text);

public sealed record DoseRangeViewModel(string MinMgPerKg, string MaxMgPerKg);

public sealed record FormularyInteractionViewModel(
    Guid Id, Guid? InteractsWithMedicineId, string? InteractsWithMedicineName, string? InteractsWithTerm, string Severity, string Note);

public sealed record FormularyLinkedSkuViewModel(Guid SkuId, string ProductName, string VariantLabel);

/// <summary>A formulary list row (§12.4). <c>IsStocked</c> is "has linked SKUs".</summary>
public sealed record FormularyMedicineSummaryViewModel(
    Guid Id, string Code, string Name, string Form, string StrengthText, string DosingBasis, bool IsControlled, bool IsActive, bool IsStocked);

public sealed record FormularyMedicineDetailViewModel(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<string> ActiveIngredients,
    IReadOnlyList<string> DrugClasses,
    string Form,
    StrengthViewModel Strength,
    string DefaultRoute,
    IReadOnlyList<string> AllowedRoutes,
    string DosingBasis,
    DoseRangeViewModel? DoseRange,
    string Contraindications,
    IReadOnlyList<FormularyInteractionViewModel> Interactions,
    string DispensingUnit,
    bool IsControlled,
    string? ControlledSchedule,
    bool IsActive,
    IReadOnlyList<FormularyLinkedSkuViewModel> LinkedSkus,
    int Version);

/// <summary>A SKU in the editor's linked-SKU picker, clinic-only SKUs included (§12.4).</summary>
public sealed record FormularySkuSearchResultViewModel(
    Guid SkuId, string ProductName, string VariantLabel, string? Barcode, bool IsClinicOnly, Guid? LinkedMedicineId);
