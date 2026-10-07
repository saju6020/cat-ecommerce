namespace CatEcommerce.Read.ViewModels;

/// <summary>
/// GET /api/cats, Q2. Carries isHighRisk (and, since the ACCEPTANCE gate's A9 fix, 2026-09-27,
/// handlingNote) so no list screen needs a second call for the warning badge (§4.1/HO-CAT-002 §3 --
/// HO-CAT-001 §12.3 previously specified isHighRisk alone; §4.1 wins and §12.3 is corrected).
/// </summary>
public sealed record CatListItemViewModel(
    Guid Id,
    Guid OwnerId,
    string Name,
    string Species,
    string? Breed,
    string Sex,
    bool IsHighRisk,
    string? HandlingNote,
    string LifeStatus);
