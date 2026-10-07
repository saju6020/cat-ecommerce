using CatEcommerce.Shared.SharedDto.Cat;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatMedicalProfileDto(
    IReadOnlyList<AllergyInput> Allergies,
    IReadOnlyList<ChronicConditionInput> ChronicConditions,
    IReadOnlyList<MedicationInput> Medications,
    string? DietaryNotes,
    string? BehaviouralNotes,
    UserContext UserContext,
    Guid CorrelationId);
