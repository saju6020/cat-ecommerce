using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>Who is acting and under which correlation -- carried into every aggregate method that raises an event (HO-PHM-001 §11.3).</summary>
public sealed record PharmacyActor(UserContext UserContext, Guid CorrelationId)
{
    public Guid UserId => UserContext.UserId;
}

public sealed record StrengthInput(decimal Amount, StrengthUnit Unit, decimal PerQuantity, DispensingUnit PerUnit);

public sealed record DoseRangeInput(decimal MinMgPerKg, decimal MaxMgPerKg);

/// <summary>One interaction a formulary medicine lists: exactly one of <paramref name="InteractsWithMedicineId"/> and <paramref name="InteractsWithTerm"/>.</summary>
public sealed record InteractionInput(Guid? InteractsWithMedicineId, string? InteractsWithTerm, InteractionSeverity Severity, string Note);

/// <summary>HO-PHM-001 §8.2 -- the editable fields of a formulary medicine (everything but its code, which is immutable).</summary>
public sealed record FormularyMedicineDto(
    string Name,
    IReadOnlyList<string> ActiveIngredients,
    IReadOnlyList<string> DrugClasses,
    MedicineForm Form,
    StrengthInput Strength,
    AdministrationRoute DefaultRoute,
    IReadOnlyList<AdministrationRoute> AllowedRoutes,
    DosingBasis DosingBasis,
    DoseRangeInput? DoseRange,
    string Contraindications,
    IReadOnlyList<InteractionInput> Interactions,
    DispensingUnit DispensingUnit,
    IReadOnlyList<Guid> LinkedSkuIds,
    PharmacyActor Actor);

/// <summary>An interaction as the safety assessor reads it (§8.6).</summary>
public sealed record InteractionSnapshot(Guid? InteractsWithMedicineId, string? InteractsWithTerm, InteractionSeverity Severity, string Note);

/// <summary>
/// HO-PHM-001 §8.9 -- a formulary medicine as the rest of the solution reads it (<c>IFormularyReader</c>, the safety assessor, the prescribing
/// handlers). A value, never the aggregate: nothing outside Pharmacy rehydrates a <c>FormularyMedicineAggregate</c> (ADR-151).
/// </summary>
public sealed record FormularyMedicineSnapshot(
    Guid MedicineId,
    string Code,
    string Name,
    MedicineForm Form,
    Strength Strength,
    AdministrationRoute DefaultRoute,
    IReadOnlyList<AdministrationRoute> AllowedRoutes,
    DosingBasis DosingBasis,
    DoseRange? DoseRange,
    string Contraindications,
    IReadOnlyList<string> ActiveIngredients,
    IReadOnlyList<string> DrugClasses,
    IReadOnlyList<InteractionSnapshot> Interactions,
    bool IsControlled,
    string? ControlledSchedule,
    DispensingUnit DispensingUnit,
    IReadOnlyList<Guid> LinkedSkuIds,
    bool IsActive)
{
    public MedicineSnapshot ToSnapshot() => new(Code, Name, Form, Strength.Text(), IsControlled, ControlledSchedule, DispensingUnit, LinkedSkuIds);
}

/// <summary>HO-PHM-001 §8.9 -- <c>IControlledDrugDirectory</c>'s answer: a controlled medicine and the SKUs that are it.</summary>
public sealed record ControlledMedicineRef(
    Guid MedicineId, string Name, string StrengthText, MedicineForm Form, string? ControlledSchedule, DispensingUnit DispensingUnit, IReadOnlyList<Guid> LinkedSkuIds);

/// <summary>HO-SHP-001's SKU as Pharmacy reads it through <c>ICatalogueReader</c>.</summary>
public sealed record CatalogueSku(
    Guid SkuId, Guid ProductId, string ProductName, string VariantLabel, string? Barcode, bool IsActive, bool IsClinicOnly, bool RequiresPrescription);

/// <summary>HO-SHP-001's product as Pharmacy reads it through <c>ICatalogueReader</c>.</summary>
public sealed record CatalogueProduct(
    Guid ProductId, string Name, string Brand, bool RequiresPrescription, bool IsActive, IReadOnlyList<Guid> SkuIds);
