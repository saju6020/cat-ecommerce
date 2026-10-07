namespace CatEcommerce.Read.ViewModels;

/// <summary>Feeds the medical history PDF, Q5 -- US-CAT-10. HO-CAT-001 section 12.3.</summary>
public sealed record CatMedicalHistoryViewModel(
    Guid CatId,
    string Name,
    Guid OwnerId,
    string Species,
    string? Breed,
    string Sex,
    DateOnly? DateOfBirth,
    string DateOfBirthPrecision,
    string NeuterStatus,
    DateOnly? NeuteredOn,
    bool IsHighRisk,
    string? HandlingNote,
    IReadOnlyList<CatAllergyViewModel> Allergies,
    IReadOnlyList<CatChronicConditionViewModel> ChronicConditions,
    IReadOnlyList<CatMedicationViewModel> Medications,
    IReadOnlyList<CatMedicalHistoryVaccinationViewModel> Vaccinations,
    IReadOnlyList<CatMedicalHistoryParasiteTreatmentViewModel> ParasiteTreatments,
    IReadOnlyList<CatWeightPointViewModel> WeightHistory);

public sealed record CatMedicalHistoryVaccinationViewModel(
    string VaccineName, string BatchReference, DateOnly AdministeredOn, DateOnly NextDueOn);

public sealed record CatMedicalHistoryParasiteTreatmentViewModel(
    string TreatmentType, string ProductName, DateOnly AdministeredOn, DateOnly NextDueOn);
