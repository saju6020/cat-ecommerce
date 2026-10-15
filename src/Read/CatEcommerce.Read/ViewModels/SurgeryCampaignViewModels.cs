namespace CatEcommerce.Read.ViewModels;

// HO-SRG-001 section 12.4 -- campaign price lists and their cases (US-SRG-11).

public sealed record SurgeryCampaignPriceViewModel(Guid ServiceId, string ServiceName, MoneyViewModel Price);

public sealed record SurgeryCampaignViewModel(
    Guid Id, int Version, string Code, string Name, string Description, DateOnly ValidFrom, DateOnly ValidTo, IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<SurgeryCampaignPriceViewModel> Prices, int? MaxCases, int CasesBooked, int? CasesRemaining, bool IsActive, bool PricesLocked);

public sealed record ApplicableCampaignViewModel(
    Guid Id, string Code, string Name, DateOnly ValidFrom, DateOnly ValidTo, MoneyViewModel Price, int? CasesRemaining);

public sealed record CampaignCaseViewModel(
    Guid SurgeryId, Guid CatId, string CatName, string Procedure, DateTimeOffset StartsOn, string Status, string? Outcome, MoneyViewModel CampaignPrice, MoneyViewModel? StandardPrice,
    string? EligibilityNote);

/// <summary>Totals over the NON-cancelled cases. <c>StandardTotal</c> counts only cases whose standard price could be found.</summary>
public sealed record CampaignCaseTotalsViewModel(int Count, MoneyViewModel CampaignTotal, MoneyViewModel StandardTotal);

public sealed record CampaignCasesViewModel(Guid CampaignId, string Code, string Name, IReadOnlyList<CampaignCaseViewModel> Cases, CampaignCaseTotalsViewModel Totals,
    int PageNumber, int PageSize, int TotalCases);
