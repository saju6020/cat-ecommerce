namespace CatEcommerce.Api.Contracts.Cat;

/// <summary>US-CAT-03. Replaces all three collections wholesale.</summary>
public sealed class UpdateCatMedicalProfileRequest
{
    public IReadOnlyList<AllergyInputRequest> Allergies { get; init; } = [];
    public IReadOnlyList<ChronicConditionInputRequest> ChronicConditions { get; init; } = [];
    public IReadOnlyList<MedicationInputRequest> Medications { get; init; } = [];
    public string? DietaryNotes { get; init; }
    public string? BehaviouralNotes { get; init; }
}

public sealed class AllergyInputRequest
{
    public required string Allergen { get; init; }
    public required string Reaction { get; init; }
    public required string Severity { get; init; }
    public required DateOnly RecordedOn { get; init; }
}

public sealed class ChronicConditionInputRequest
{
    public required string Condition { get; init; }
    public DateOnly? DiagnosedOn { get; init; }
    public string? Notes { get; init; }
}

public sealed class MedicationInputRequest
{
    public required string MedicationName { get; init; }
    public required string Dosage { get; init; }
    public required string Frequency { get; init; }
    public required DateOnly StartedOn { get; init; }
    public DateOnly? EndsOn { get; init; }
}
