namespace CatEcommerce.Read.ViewModels;

/// <summary>
/// GET /api/cats/{catId}/vaccination-status, Q4 -- US-CAT-07. Computed entirely read-side;
/// see CatVaccinationStatusCalculator. HO-CAT-001 section 12.3.
/// </summary>
public sealed record CatVaccinationStatusViewModel(
    Guid CatId,
    string OverallStatus,
    DateOnly? EarliestNextDueOn,
    bool BookingEligible,
    IReadOnlyList<CatVaccinationStatusItemViewModel> Vaccines);

public sealed record CatVaccinationStatusItemViewModel(
    Guid VaccineId,
    string VaccineCode,
    string VaccineName,
    string Status,
    DateOnly? NextDueOn,
    string? Reason);
