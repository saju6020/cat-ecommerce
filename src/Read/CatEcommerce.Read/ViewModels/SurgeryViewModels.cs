namespace CatEcommerce.Read.ViewModels;

// HO-SRG-001 §12.4 -- the contract HO-SRG-002 consumes. JSON camelCase; instants ISO-8601 with offset; local dates yyyy-MM-dd; money as HO-ADM-001 §12.4
// (MoneyViewModel: amount as text, currency code). Every surgery view carries branchTimeZoneId.

public sealed record SurgeryNamedRefViewModel(Guid Id, string Name);

public sealed record SurgeryServiceViewModel(
    Guid Id, string Name, string Category, int DurationMinutes, MoneyViewModel Price, bool IsSterilisation, IReadOnlyList<string> QualifiedRoleIds);

public sealed record SurgeryStaffOptionViewModel(Guid Id, string Name, IReadOnlyList<string> RoleIds);

public sealed record SurgeryStaffViewModel(IReadOnlyList<SurgeryStaffOptionViewModel> Surgeons, IReadOnlyList<SurgeryStaffOptionViewModel> Assistants);

public sealed record SurgeryStartViewModel(DateTimeOffset StartsOn, DateTimeOffset EndsOn, bool OutsideOpeningHours);

/// <summary>A busy interval and WHY, so the booking form can show why a time is missing ("Dr Rahman is operating on another surgery 10:00-11:00").</summary>
public sealed record SurgeryBusyIntervalViewModel(DateTimeOffset StartsOn, DateTimeOffset EndsOn, string Label);

public sealed record SurgeryBusyViewModel(
    IReadOnlyList<SurgeryBusyIntervalViewModel> Theatre, IReadOnlyList<SurgeryBusyIntervalViewModel> Surgeon, IReadOnlyList<SurgeryBusyIntervalViewModel> Assistant);

public sealed record SurgeryAvailabilityViewModel(
    DateOnly Date, string BranchTimeZoneId, int GridMinutes, IReadOnlyList<SurgeryStartViewModel> Starts, SurgeryBusyViewModel Busy);

public sealed record SurgeryChecklistSummaryViewModel(bool IsComplete, IReadOnlyList<string> OutstandingItems);

public sealed record SurgeryDayCatViewModel(Guid Id, string Name, bool IsHighRisk, bool IsDeceased);

public sealed record SurgeryDayCustomerViewModel(Guid Id, string Name, string? Phone);

/// <summary><c>consentRecorded</c> on the day list is the checklist's consent item (cheap, polled); the detail reads the consent records live.</summary>
public sealed record SurgeryDayItemViewModel(
    Guid SurgeryId, int Version, DateTimeOffset StartsOn, DateTimeOffset EndsOn, DateTimeOffset AdmitBy, string Status,
    SurgeryNamedRefViewModel Theatre, SurgeryNamedRefViewModel Surgeon, SurgeryNamedRefViewModel Assistant,
    SurgeryDayCatViewModel Cat, SurgeryDayCustomerViewModel Customer, string Procedure, SurgeryChecklistSummaryViewModel Checklist,
    bool ConsentRecorded, bool NeedsRescheduling, string? RecoveryStatus);

public sealed record SurgeryDayListViewModel(DateOnly Date, string BranchTimeZoneId, IReadOnlyList<SurgeryDayItemViewModel> Surgeries);

public sealed record SurgerySummaryViewModel(
    Guid Id, DateTimeOffset StartsOn, DateTimeOffset EndsOn, string Status, Guid CatId, string CatName, string Procedure, string PricingBasis, string? Outcome,
    Guid SurgeonId, string SurgeonName, bool ReportOutstanding, string BranchTimeZoneId);

public sealed record SurgeryChecklistItemViewModel(
    Guid Id, string Kind, string Status, Guid? CompletedBy, DateTimeOffset? CompletedOn, string? Note, string? WaiverReason, Guid? ReferenceId);

public sealed record SurgeryStatusChangeViewModel(string? From, string To, DateTimeOffset ChangedOn, Guid ChangedBy);

public sealed record SurgeryRecoveryChangeViewModel(string? From, string To, DateTimeOffset ChangedOn, Guid ChangedBy, string? ChangedByName, string? Note);

public sealed record SurgeryFollowUpViewModel(Guid Id, string Kind, DateOnly DueOn, DateOnly WindowFrom, DateOnly WindowTo, string Status, DateTimeOffset? ClosedOn);

public sealed record SurgeryPriceViewModel(MoneyViewModel Amount, string Basis, Guid? CampaignId, string? CampaignEligibilityNote);

public sealed record SurgeryProcedureViewModel(string Name, string Category, int DurationMinutes, bool IsSterilisation);

public sealed record SurgeryConsentOverrideViewModel(Guid OverriddenBy, string? OverriddenByName, DateTimeOffset OverriddenOn, string Reason);

public sealed record SurgeryStartBlockerViewModel(string Code, string Message);

public sealed record SurgeryLatestWeightViewModel(decimal Kilograms, DateOnly RecordedOn);

public sealed record SurgeryDetailViewModel(
    Guid Id, int Version, Guid BranchId, string BranchTimeZoneId, string Status, Guid CatId, string CatName, bool CatIsDeceased, Guid CustomerId, string CustomerName, string? CustomerPhone,
    Guid VisitId, Guid AppointmentId, SurgeryProcedureViewModel Procedure, SurgeryPriceViewModel PriceSnapshot,
    SurgeryNamedRefViewModel Theatre, SurgeryNamedRefViewModel Surgeon, SurgeryNamedRefViewModel Assistant,
    DateTimeOffset StartsOn, DateTimeOffset EndsOn, DateTimeOffset AdmitBy, string FastingInstructions,
    string? AsaGrade, bool AsaIsEmergency, SurgeryConsentOverrideViewModel? ConsentOverride,
    DateTimeOffset? AdmittedOn, DateTimeOffset? ProcedureStartedOn, DateTimeOffset? ProcedureEndedOn, DateTimeOffset? DischargedOn,
    string? Outcome, string? RecoveryStatus, string? PostOpInstructions, DateTimeOffset? CancelledOn, string? CancellationReason,
    IReadOnlyList<SurgeryChecklistItemViewModel> Checklist, SurgeryChecklistSummaryViewModel ChecklistSummary, bool ConsentRecorded,
    IReadOnlyList<SurgeryStartBlockerViewModel> StartBlockers, IReadOnlyList<string> AllowedTransitions,
    IReadOnlyList<SurgeryStatusChangeViewModel> StatusHistory, IReadOnlyList<SurgeryRecoveryChangeViewModel> RecoveryHistory, IReadOnlyList<SurgeryFollowUpViewModel> FollowUps,
    string ReportStatus, bool? NeuterStatusApplied, SurgeryLatestWeightViewModel? LatestWeight);
