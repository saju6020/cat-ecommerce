namespace CatEcommerce.Read.ViewModels;

// HO-SRG-001 section 12.4 -- recovery board, discharge draft, portal and contract views (US-SRG-07, US-SRG-10).

public sealed record RecoveryBoardEntryViewModel(
    Guid SurgeryId, Guid CatId, string CatName, bool CatIsHighRisk, string CustomerName, string? CustomerPhone, string Procedure, DateTimeOffset? ProcedureEndedOn,
    string RecoveryStatus, DateTimeOffset? StatusChangedOn, Guid? StatusChangedBy, string? StatusChangedByName);

public sealed record RecoveryBoardViewModel(DateTimeOffset GeneratedOn, IReadOnlyList<RecoveryBoardEntryViewModel> Entries);

public sealed record DischargeDraftFollowUpViewModel(string Kind, DateOnly DueOn, DateOnly WindowFrom, DateOnly WindowTo);

public sealed record DischargeDraftViewModel(
    Guid SurgeryId, string Status, string? RecoveryStatus, bool CatIsDeceased, string PostOpInstructions, IReadOnlyList<DischargeDraftFollowUpViewModel> FollowUps);

public sealed record CustomerSurgerySummaryViewModel(
    Guid Id, string CatName, string Procedure, DateTimeOffset StartsOn, DateTimeOffset AdmitBy, string Status, string Branch, string BranchTimeZoneId);

public sealed record CustomerSurgeryFollowUpViewModel(string Kind, DateOnly DueOn, string Status);

public sealed record CustomerSurgeryDetailViewModel(
    Guid Id, Guid CatId, string CatName, string Procedure, DateTimeOffset StartsOn, DateTimeOffset AdmitBy, string Status, string Branch, string BranchTimeZoneId,
    string FastingInstructions, string? PostOpInstructions, IReadOnlyList<CustomerSurgeryFollowUpViewModel> FollowUps, string BookFollowUpLink);

public sealed record CatSurgeryHistoryViewModel(
    Guid SurgeryId, Guid? VisitId, string Procedure, bool IsSterilisation, DateTimeOffset StartsOn, string Status, string? Outcome, DateTimeOffset? DischargedOn, string ReportStatus);

public sealed record SurgeryChargeableDrugViewModel(Guid EntryId, Guid? MedicineId, string? MedicineName, Guid SkuId, decimal Quantity, string? StockUnit);

public sealed record SurgeryChargeableItemsViewModel(
    Guid SurgeryId, Guid CatId, Guid CustomerId, Guid BranchId, Guid ServiceId, string ProcedureName, MoneyViewModel Price, string PricingBasis, Guid? CampaignId,
    IReadOnlyList<SurgeryChargeableDrugViewModel> Drugs);
