using CatEcommerce.Read.ClinicalHistory;

namespace CatEcommerce.Read.ViewModels;

// HO-EMR-001 §8.7 / §12.4 -- the clinical history of one cat.

/// <summary>The cat header data the history drawer needs. No owner or customer detail: the history is the cat's.</summary>
public sealed record ClinicalHistoryCatViewModel(
    Guid Id, string Name, string Sex, DateOnly? DateOfBirth, string? Breed, bool IsHighRisk, string LifeStatus);

/// <summary>A problem that is still current: the latest certainty recorded for its code over the last 12 months.</summary>
public sealed record ClinicalHistoryProblemViewModel(
    Guid DiagnosisCodeId, string Code, string Name, string Certainty, DateTimeOffset LastSeen);

/// <summary><c>GET /api/staff/cats/{catId}/clinical-history</c> -- everything the drawer opens with, in one call.</summary>
public sealed record ClinicalHistorySummaryViewModel(
    ClinicalHistoryCatViewModel Cat,
    IReadOnlyList<ClinicalHistoryProblemViewModel> ActiveProblems,
    IReadOnlyList<ClinicalHistoryItem> RecentVisits,
    VitalsEntryViewModel? LastVitals,
    IReadOnlyList<CatWeightPointViewModel> WeightSeries,
    CatVaccinationStatusViewModel? VaccinationStatus,
    IReadOnlyList<DiagnosticOrderListItemViewModel> PendingDiagnostics,
    IReadOnlyList<ClinicalHistorySectionSummary> Sections);

/// <summary><c>GET /api/staff/cats/{catId}/clinical-history/{section}</c> -- a page of a section, or
/// <c>available: false</c> with the owning area when no provider is registered for it yet.</summary>
public sealed record ClinicalHistorySectionViewModel(
    string Key, bool Available, string Owner, IReadOnlyList<ClinicalHistoryItem> Items, int PageNumber, int PageSize, int TotalCount);
