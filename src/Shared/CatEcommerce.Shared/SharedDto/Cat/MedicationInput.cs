namespace CatEcommerce.Shared.SharedDto.Cat;

public sealed record MedicationInput(
    string MedicationName,
    string Dosage,
    string Frequency,
    DateOnly StartedOn,
    DateOnly? EndsOn);
