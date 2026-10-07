using CatEcommerce.Shared.SharedDto.Cat;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class UpdateCatMedicalProfileCommand : Command
{
    public required Guid CatId { get; init; }
    public required IReadOnlyList<AllergyInput> Allergies { get; init; }
    public required IReadOnlyList<ChronicConditionInput> ChronicConditions { get; init; }
    public required IReadOnlyList<MedicationInput> Medications { get; init; }
    public string? DietaryNotes { get; init; }
    public string? BehaviouralNotes { get; init; }
}
