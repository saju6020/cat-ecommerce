using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Prescription;

// HO-PHM-001 §11.1 C1-C4. Data only: ids are minted in the controller, enumerations travel as their display names and are parsed by the mapper.
// Every command is dispatched with ValidateCommand = true (F3).

public sealed record StrengthModel(decimal Amount, string Unit, decimal PerQuantity, string PerUnit);

public sealed record DoseRangeModel(decimal MinMgPerKg, decimal MaxMgPerKg);

public sealed record InteractionModel(Guid? InteractsWithMedicineId, string? InteractsWithTerm, string Severity, string Note);

/// <summary>The editable fields of a formulary medicine -- shared by create and update.</summary>
public abstract class FormularyMedicineCommandBase : Command
{
    public required Guid MedicineId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> ActiveIngredients { get; init; }
    public IReadOnlyList<string> DrugClasses { get; init; } = [];
    public required string Form { get; init; }
    public required StrengthModel Strength { get; init; }
    public required string DefaultRoute { get; init; }
    public required IReadOnlyList<string> AllowedRoutes { get; init; }
    public required string DosingBasis { get; init; }
    public DoseRangeModel? DoseRange { get; init; }
    public string Contraindications { get; init; } = "";
    public IReadOnlyList<InteractionModel> Interactions { get; init; } = [];
    public required string DispensingUnit { get; init; }
    public IReadOnlyList<Guid> LinkedSkuIds { get; init; } = [];
}

/// <summary>C1 -- an administrator adds a medicine to the formulary (US-PHM-04 AC1). The code is immutable once created.</summary>
public sealed class CreateFormularyMedicineCommand : FormularyMedicineCommandBase
{
    public required string Code { get; init; }
}

/// <summary>C2 -- an administrator edits a medicine. Two administrators editing from the same version: the second gets <c>409</c>.</summary>
public sealed class UpdateFormularyMedicineCommand : FormularyMedicineCommandBase
{
    public required int ExpectedVersion { get; init; }
}

/// <summary>C3 -- the controlled flag, a deliberate act with its own audit action. Clearing it on a medicine with register entries needs a reason.</summary>
public sealed class SetFormularyMedicineControlledCommand : Command
{
    public required Guid MedicineId { get; init; }
    public required bool IsControlled { get; init; }
    public string? ControlledSchedule { get; init; }
    public string? Reason { get; init; }
}

/// <summary>C4 -- an inactive medicine is not prescribable; existing prescriptions stay dispensable.</summary>
public sealed class SetFormularyMedicineActiveCommand : Command
{
    public required Guid MedicineId { get; init; }
    public required bool IsActive { get; init; }
}
