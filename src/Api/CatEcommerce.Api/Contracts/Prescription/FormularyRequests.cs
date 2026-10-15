namespace CatEcommerce.Api.Contracts.Prescription;

// HO-PHM-001 §12.4 -- request bodies of the formulary endpoints. The controller mints the medicine id and attaches the caller (never taken from the body).

public sealed class StrengthRequest
{
    public decimal Amount { get; init; }
    public string Unit { get; init; } = "";
    public decimal PerQuantity { get; init; } = 1m;
    public string PerUnit { get; init; } = "";
}

public sealed class DoseRangeRequest
{
    public decimal MinMgPerKg { get; init; }
    public decimal MaxMgPerKg { get; init; }
}

public sealed class InteractionRequest
{
    public Guid? InteractsWithMedicineId { get; init; }
    public string? InteractsWithTerm { get; init; }
    public string Severity { get; init; } = "";
    public string Note { get; init; } = "";
}

/// <summary><c>POST /api/admin/formulary</c> (C1) and <c>PUT /api/admin/formulary/{medicineId}</c> (C2).</summary>
public sealed class FormularyMedicineRequest
{
    /// <summary>Create only; immutable afterwards.</summary>
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public List<string> ActiveIngredients { get; init; } = [];
    public List<string> DrugClasses { get; init; } = [];
    public string Form { get; init; } = "";
    public StrengthRequest Strength { get; init; } = new();
    public string DefaultRoute { get; init; } = "";
    public List<string> AllowedRoutes { get; init; } = [];
    public string DosingBasis { get; init; } = "";
    public DoseRangeRequest? DoseRange { get; init; }
    public string Contraindications { get; init; } = "";
    public List<InteractionRequest> Interactions { get; init; } = [];
    public string DispensingUnit { get; init; } = "";
    public List<Guid> LinkedSkuIds { get; init; } = [];

    /// <summary>Update only: the <c>version</c> the editor loaded.</summary>
    public int ExpectedVersion { get; init; }
}

/// <summary><c>PUT /api/admin/formulary/{medicineId}/controlled</c> (C3).</summary>
public sealed class SetControlledRequest
{
    public bool IsControlled { get; init; }
    public string? ControlledSchedule { get; init; }
    public string? Reason { get; init; }
}

/// <summary><c>PUT /api/admin/formulary/{medicineId}/active</c> (C4).</summary>
public sealed class SetActiveRequest
{
    public bool IsActive { get; init; }
}
