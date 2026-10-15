namespace CatEcommerce.Read.ViewModels;

// HO-BRD-001 section 12.4 / 12.6 (Phase F) -- incidents, the owner's diary and the dashboard.

public sealed record IncidentViewModel(
    Guid Id, Guid StayId, Guid CatId, string CatName, string Kind, string Severity, string Description, string ImmediateAction, DateTimeOffset OccurredOn, DateTimeOffset ReportedOn,
    bool OwnerNotified, DateTimeOffset? OwnerAcknowledgedOn, string? AcknowledgedVia, DateTimeOffset? EscalatedOn, DateTimeOffset? PhoneCallOn, string? PhoneCallOutcome,
    string Status, DateTimeOffset? ResolvedOn, string? Resolution, IReadOnlyList<DiaryMediaViewModel> Media);

public sealed record DiaryMediaViewModel(Guid Id, Guid? CatId, string? Caption, string Status, StoredFileViewModel? File);

public sealed record DiaryObservationViewModel(Guid CatId, string CatName, DateTimeOffset RecordedOn, string PortionEaten, string WaterIntake, string LitterOutput, string Mood, string? Notes);

/// <summary>One day of the owner's diary (FR-POR-4): observations, media and how many care tasks were completed.</summary>
public sealed record DiaryDayViewModel(
    DateOnly Date, IReadOnlyList<DiaryObservationViewModel> Observations, IReadOnlyList<DiaryMediaViewModel> Media, int CompletedTasks);

public sealed record StayDiaryViewModel(Guid StayId, DateOnly From, DateOnly To, IReadOnlyList<DiaryDayViewModel> Days);

public sealed record DashboardOccupancyTypeViewModel(string UnitType, int UnitsOccupied, int UnitsActive);

public sealed record DashboardOccupancyViewModel(int UnitsOccupied, int UnitsActive, int CatsInHouse, IReadOnlyList<DashboardOccupancyTypeViewModel> ByType);

public sealed record DashboardStayViewModel(Guid StayId, IReadOnlyList<string> CatNames, string? UnitCode, string Status, string? DepositState);

public sealed record DashboardCatViewModel(Guid StayId, Guid CatId, string CatName, string? UnitCode);

public sealed record DashboardTaskViewModel(Guid TaskId, Guid StayId, string CatName, string Title, DateTimeOffset DueOn, Guid? AssignedCaretakerId);

public sealed record DashboardIncidentViewModel(Guid IncidentId, Guid StayId, string CatName, string Severity, DateTimeOffset ReportedOn, bool Acknowledged, bool Escalated);

public sealed record DashboardBalanceViewModel(Guid StayId, IReadOnlyList<string> CatNames, string Status, MoneyViewModel Amount, DateTimeOffset? DepositDueBy, string Reason);

/// <summary>US-BRD-19 AC1: every section for the active branch and today (branch-local), in one side-effect-free read.</summary>
public sealed record BoardingDashboardViewModel(
    DateOnly Today, DateTimeOffset GeneratedOn, DashboardOccupancyViewModel Occupancy, IReadOnlyList<DashboardStayViewModel> Arrivals, IReadOnlyList<DashboardStayViewModel> Departures,
    IReadOnlyList<DashboardCatViewModel> NotEating, IReadOnlyList<DashboardTaskViewModel> OverdueTasks, IReadOnlyList<DashboardTaskViewModel> UnassignedTasks,
    IReadOnlyList<DashboardIncidentViewModel> OpenIncidents, IReadOnlyList<DashboardIncidentViewModel> CallOwner,
    IReadOnlyList<DashboardBalanceViewModel> UnpaidBalances, IReadOnlyList<DashboardBalanceViewModel> RefundDue, IReadOnlyList<DashboardStayViewModel> Attention);

// ── Phase G: the stay summary, relocation options and the worker's invoicing list ─────────────────────────────────

public sealed record SummaryObservationViewModel(DateOnly Date, string PortionEaten, string WaterIntake, string LitterOutput, string Mood, string? Notes);

public sealed record SummaryIncidentViewModel(Guid Id, string Kind, string Severity, DateTimeOffset OccurredOn, string Status, string? Resolution);

public sealed record SummaryMedicationViewModel(string Title, DateTimeOffset GivenOn, string? GivenBy);

public sealed record SummaryVetVisitViewModel(Guid ReferralId, string Reason, string Urgency, DateTimeOffset ReferredOn, DateTimeOffset? ReturnedOn, Guid? VisitId);

public sealed record SummaryBelongingViewModel(string Kind, string Description, bool? Returned, string? Note);

/// <summary>One cat's stay in review (US-BRD-17 AC1): observations by day, weights and the change, incidents, medications given, vet visits, belongings and the check-out signature.</summary>
public sealed record SummaryCatViewModel(
    Guid CatId, string CatName, decimal? WeightIn, decimal? WeightOut, decimal? WeightChange, IReadOnlyList<SummaryObservationViewModel> Observations,
    IReadOnlyList<SummaryIncidentViewModel> Incidents, IReadOnlyList<SummaryMedicationViewModel> Medications, IReadOnlyList<SummaryVetVisitViewModel> VetVisits,
    IReadOnlyList<SummaryBelongingViewModel> Belongings, string? CheckOutSignerName, DateTimeOffset? CheckOutSignedOn);

public sealed record SummaryChargeViewModel(string LineRef, string Description, MoneyViewModel Net, bool Billed);

public sealed record SummaryBillingViewModel(MoneyViewModel BilledTotal, MoneyViewModel UnbilledTotal, MoneyViewModel InvoicedOutstanding, IReadOnlyList<Guid> InvoiceIds);

public sealed record StayReportViewModel(
    Guid StayId, string Status, DateOnly ArrivalOn, DateOnly DepartureOn, int Nights, DateTimeOffset? CheckedOutOn, IReadOnlyList<SummaryCatViewModel> Cats,
    IReadOnlyList<SummaryChargeViewModel> Charges, MoneyViewModel DepositReceived, SummaryBillingViewModel Billing, MoneyViewModel? CreditDue);

public sealed record RelocationOptionViewModel(Guid UnitId, string Code, string Name, string UnitType, string? Block);

public sealed record StayInvoiceDueViewModel(Guid StayId, Guid BranchId, Guid CustomerId, int Cycle, DateOnly ServiceDateTo);

/// <summary>One charge line of the preview: <c>Keep</c> (stays as it is, invoiced or not), <c>Void</c> (an uninvoiced charge that no longer applies) or <c>Record</c> (a new charge).</summary>
public sealed record EarlyCheckoutLineViewModel(string LineRef, string Description, MoneyViewModel Net, string Action);

/// <summary>The early check-out preview (shape agreed with BRD-002's <c>CheckOutPreview</c>: isEarly, nightsBooked, nightsCharged, quote, lines, credit, creditOutcome; the rest is extra). <c>CreditOutcome</c> is None, Credit (the settings say refund as credit) or Refund. <c>IsEarly=false</c> means the check-out today changes no charge. <c>Credit</c> is owed for invoiced charges that would no longer apply.</summary>
public sealed record EarlyCheckoutPreviewViewModel(
    bool IsEarly, int NightsBooked, int NightsCharged, QuoteViewModel? Quote, IReadOnlyList<EarlyCheckoutLineViewModel> Lines, MoneyViewModel Credit, string CreditOutcome,
    DateOnly Today, DateOnly BookedDepartureOn, DateOnly ChargedDepartureOn, DateTimeOffset UnitFreeFrom, MoneyViewModel PreviousTotal, MoneyViewModel NewTotal, bool IsLate = false);

// ── Phase H: waitlist and daycare ──────────────────────────────────────────────────────────────────────────────────

public sealed record BoardingWaitlistEntryViewModel(Guid Id, Guid BranchId, DateOnly ArrivalOn, DateOnly DepartureOn, int CatCount, string? UnitType, string Status, DateTimeOffset JoinedOn, DateTimeOffset? NotifiedOn);

public sealed record DaycareVisitViewModel(
    Guid Id, Guid BranchId, Guid CustomerId, string? CustomerName, Guid CatId, string CatName, DateOnly LocalDate, int SlotNumber, string Basis, int? ExpectedHours, string Status,
    DateTimeOffset? CheckedInOn, DateTimeOffset? CheckedOutOn, MoneyViewModel? ChargeAmount);

public sealed record DaycareDayViewModel(DateOnly Date, int Capacity, int SlotsTaken, bool Offered, IReadOnlyList<DaycareVisitViewModel> Visits);

public sealed record DaycareRateCardViewModel(Guid Id, MoneyViewModel HourlyRate, MoneyViewModel DayRate, Guid TaxClassId, DateTimeOffset EffectiveFrom, string Status);

public sealed record DailyUpdateCandidateViewModel(Guid StayId, DateOnly LocalDate);
