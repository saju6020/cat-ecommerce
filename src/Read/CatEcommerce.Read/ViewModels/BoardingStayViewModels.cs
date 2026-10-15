namespace CatEcommerce.Read.ViewModels;

// HO-BRD-001 section 12.4 -- the stay and availability reads. Money is MoneyViewModel; enumerations are display names; local dates yyyy-MM-dd.

public sealed record QuoteUnitViewModel(int Sequence, string Kind, int FromNight, int ToNight, string Description, MoneyViewModel Amount);

public sealed record QuoteDiscountLineViewModel(int Sequence, string Description, MoneyViewModel Amount);

/// <summary>The itemised quote (US-BRD-03 AC2): rate units and discount lines are SEPARATE, never merged.</summary>
public sealed record QuoteViewModel(
    string Basis, IReadOnlyList<QuoteUnitViewModel> Units, IReadOnlyList<QuoteDiscountLineViewModel> DiscountLines,
    MoneyViewModel Gross, MoneyViewModel DiscountTotal, MoneyViewModel Total);

public sealed record AvailabilityUnitTypeViewModel(string UnitType, int CapacityCats, int FreeUnitCount, QuoteViewModel Quote);

public sealed record AvailabilityUnitViewModel(Guid UnitId, string Code, string UnitType, int CapacityCats, string? Block, bool IsFree);

public sealed record AvailabilityAlternativeViewModel(DateOnly ArrivalOn, DateOnly DepartureOn, string UnitType, MoneyViewModel Total);

public sealed record AvailabilityViolationViewModel(string ErrorCode, string Message);

/// <summary><paramref name="Units"/> is filled for staff only (per-unit freedom, incl. isolation units).</summary>
public sealed record BoardingAvailabilityViewModel(
    string BranchTimeZoneId, int Nights, DateTimeOffset? OccupiedFrom, DateTimeOffset? OccupiedTo, AvailabilityViolationViewModel? Violation,
    IReadOnlyList<AvailabilityUnitTypeViewModel> UnitTypes, IReadOnlyList<AvailabilityUnitViewModel>? Units,
    IReadOnlyList<AvailabilityAlternativeViewModel> Alternatives, bool WaitlistAvailable);

public sealed record GateReasonViewModel(string VaccineCode, string VaccineName, string Reason, DateOnly? DueOn);

public sealed record StayCatGateViewModel(string Status, IReadOnlyList<GateReasonViewModel> Reasons, DateOnly? EvaluatedOn, bool Overridden, string? OverrideReason, DateTimeOffset? OverriddenOn);

public sealed record FeedingViewModel(string Time, string Portion, string FoodSource, string FoodDescription, Guid? HouseFoodSkuId);

public sealed record MedicationViewModel(string Name, string Dose, string Route, IReadOnlyList<string> Times, string? Instructions, DateOnly? FromOn, DateOnly? ToOn);

public sealed record EmergencyContactViewModel(string Name, string Phone, string? Relationship);

public sealed record CareProfileViewModel(
    IReadOnlyList<FeedingViewModel> Feedings, IReadOnlyList<MedicationViewModel> Medications, string LitterType, string? LitterNote,
    bool VetVisitAuthorised, MoneyViewModel VetSpendingLimit, EmergencyContactViewModel EmergencyContact, string? Notes);

public sealed record BelongingViewModel(Guid Id, string Kind, string Description, IReadOnlyList<Guid> FileIds, bool? Returned, string? ReturnNote);

/// <summary>What was recorded at check-in (US-BRD-08) and, once out, at check-out (US-BRD-17). The signature is reported as who and when, never as strokes.</summary>
public sealed record StayCatCheckInViewModel(
    DateTimeOffset CheckedInOn, decimal WeightKg, string? ArrivalHealthObservation, bool NoBelongings, IReadOnlyList<BelongingViewModel> Belongings,
    string? SignerName, DateTimeOffset? SignedOn, DateTimeOffset? CheckedOutOn, decimal? CheckOutWeightKg, string? CheckOutSignerName);

public sealed record StayCatViewModel(
    Guid CatId, string CatName, bool CatIsHighRisk, string? HandlingNote, StayCatGateViewModel Gate, CareProfileViewModel? CareProfile,
    bool SuspectedInfectious, string? InfectiousNote, StayCatCheckInViewModel? CheckIn, string? AwayReason = null);

public sealed record IsolationEntryViewModel(Guid Id, Guid UnitId, Guid StaffAccountId, string? StaffName, DateTimeOffset EnteredOn, DateTimeOffset? LeftOn, string Purpose);

public sealed record StayUnitViewModel(Guid UnitId, string Code, string Name, string UnitType, string? Block);

public sealed record StayDepositViewModel(MoneyViewModel Required, string State, DateTimeOffset? DueBy, MoneyViewModel Received, MoneyViewModel Pending, MoneyViewModel Outstanding);

public sealed record StayStatusChangeViewModel(string From, string To, DateTimeOffset ChangedOn, Guid ChangedBy);

public sealed record StayAllocationViewModel(Guid UnitId, string UnitType, DateTimeOffset OccupiedFrom, DateTimeOffset OccupiedTo, bool IsActive, string? Reason);

/// <summary>One stay as both the customer portal and the staff screens read it. Staff-only members are null for a customer.</summary>
public sealed record StayViewModel(
    Guid Id, int Version, Guid BranchId, string BranchTimeZoneId, Guid CustomerId, string? CustomerName, string Status, IReadOnlyList<string> AllowedTransitions,
    string Channel, DateOnly ArrivalOn, DateOnly DepartureOn, DateTimeOffset OccupiedFrom, DateTimeOffset OccupiedTo, int Nights, string RequestedUnitType,
    StayUnitViewModel? Unit, bool RequiresIsolation, bool NeedsAttention, string? AttentionReason, QuoteViewModel? Quote, StayDepositViewModel Deposit,
    MoneyViewModel? BalanceDue, IReadOnlyList<StayCatViewModel> Cats, IReadOnlyList<StayStatusChangeViewModel> StatusHistory,
    DateTimeOffset? CancelledOn, string? CancellationReason, bool DepositForfeited,
    IReadOnlyList<StayAllocationViewModel>? Allocations, IReadOnlyList<IsolationEntryViewModel>? IsolationEntries, bool InIsolationUnit,
    IReadOnlyList<StayReferralViewModel> Referrals, IReadOnlyList<StayApprovalViewModel> Approvals, IReadOnlyList<StayChargeViewModel>? Charges, MoneyViewModel? CreditDue,
    IReadOnlyList<CatHeadroomViewModel>? Headroom, string? AttentionClearedNote, IReadOnlyList<StayIncidentViewModel> Incidents);

/// <summary>An incident in the stay's detail. The owner sees medium and high ones only (a low incident is internal).</summary>
public sealed record StayIncidentViewModel(Guid Id, Guid CatId, string Kind, string Severity, DateTimeOffset OccurredOn, bool OwnerAcknowledged, string Status);

/// <summary>A clinic referral of a boarded cat (US-BRD-14). <c>VisitId</c> arrives when HO-EMR-001 opens the visit.</summary>
public sealed record StayReferralViewModel(
    Guid Id, Guid CatId, string CatName, string Reason, string Urgency, DateTimeOffset ReferredOn, Guid? VisitId, DateTimeOffset? ReturnedOn);

/// <summary>A vet charge above the cat's spending limit and the owner's answer (US-BRD-14 AC3).</summary>
public sealed record StayApprovalViewModel(
    Guid Id, Guid CatId, string CatName, MoneyViewModel Amount, string Description, string Status, DateTimeOffset RequestedOn, DateTimeOffset? DecidedOn, string? DecidedVia, string? DecisionNote);

/// <summary>One boarding charge as Billing holds it; <c>Billed</c> when an invoice carries it.</summary>
public sealed record StayChargeViewModel(Guid ChargeId, string LineRef, string Description, MoneyViewModel Net, DateOnly ServiceDate, bool Billed, bool Voided);

public sealed record CatHeadroomViewModel(Guid CatId, string CatName, bool VetVisitAuthorised, MoneyViewModel Limit, MoneyViewModel Committed, MoneyViewModel Headroom);

public sealed record StaySummaryViewModel(
    Guid Id, string Status, DateOnly ArrivalOn, DateOnly DepartureOn, int Nights, IReadOnlyList<string> CatNames, string RequestedUnitType,
    string? UnitCode, string DepositState, MoneyViewModel? BalanceDue, Guid CustomerId, string? CustomerName);

/// <summary><c>CatNames</c> are the cats of the occupying stay; <c>InTurnaround</c> is true when the day falls entirely inside the cleaning turnaround after the stay's departure.</summary>
public sealed record OccupancyCellViewModel(DateOnly Date, string State, Guid? StayId, string? Label, IReadOnlyList<string>? CatNames = null, bool InTurnaround = false);

public sealed record OccupancyRowViewModel(Guid UnitId, string Code, string Name, string UnitType, string? Block, bool IsActive, IReadOnlyList<OccupancyCellViewModel> Cells);

public sealed record BoardingOccupancyViewModel(DateOnly From, DateOnly To, IReadOnlyList<OccupancyRowViewModel> Units);
