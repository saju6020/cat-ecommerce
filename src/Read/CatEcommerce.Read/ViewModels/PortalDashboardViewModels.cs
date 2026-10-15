namespace CatEcommerce.Read.ViewModels;

// HO-POR-001 §12.4. Every type here is the portal's OWN shape (ADR-259): a section reader maps an upstream ViewModel member by member
// into these, so a field added upstream -- possibly staff-only -- cannot leak into the dashboard. No property holds another module's type.
// Clinical content never appears here (ADR-262); see PortalConventionTests.

/// <summary>One dashboard section. <see cref="State"/> is a PortalSectionState display name. <see cref="Data"/> is <c>null</c> when the
/// state is <c>Unavailable</c> -- never an empty list, never a zero (ADR-260).</summary>
public sealed record PortalSection<T>(string State, T? Data) where T : class;

/// <summary>The dashboard. A section appears here only once the area that owns its data has been merged and its reader built
/// (HO-POR-001 §17); the JSON simply has no key for a section that does not exist yet.</summary>
public sealed record PortalDashboardViewModel(
    DateTimeOffset GeneratedOn,
    PortalSection<PortalCustomerViewModel> Customer,
    PortalSection<PortalCatsViewModel> Cats,
    PortalSection<PortalAppointmentsViewModel> Appointments,
    PortalSection<PortalBalancesViewModel> Balances,
    PortalSection<PortalVisitsViewModel> Visits,
    PortalSection<PortalPrescriptionsViewModel> Prescriptions,
    PortalSection<PortalOrdersViewModel> Orders);

public sealed record PortalCustomerViewModel(string FullName);

// ── Cats ────────────────────────────────────────────────────────────────────────────────────────────
public sealed record PortalCatsViewModel(bool HasCats, bool HasMore, IReadOnlyList<PortalCatItemViewModel> Items);

/// <summary>Name and flags only -- no photo (F32, OQ-10), and no allergies, conditions, medications or handling note (ADR-262).</summary>
public sealed record PortalCatItemViewModel(
    Guid CatId, string Name, bool IsHighRisk, string VaccinationState, PortalVaccinationViewModel? Vaccination);

public sealed record PortalVaccinationViewModel(string OverallStatus, DateOnly? EarliestNextDueOn, bool BookingEligible);

// ── Appointments ────────────────────────────────────────────────────────────────────────────────────
public sealed record PortalAppointmentsViewModel(IReadOnlyList<PortalAppointmentItemViewModel> Items);

public sealed record PortalAppointmentItemViewModel(
    Guid AppointmentId, Guid CatId, string CatName, string ServiceName, DateTimeOffset? StartsOn, DateTimeOffset? EndsOn,
    string? PerformerName, string BranchName, string BranchTimeZoneId, string Status);

// ── Visits (identifying metadata only, ADR-262) ─────────────────────────────────────────────────────
public sealed record PortalVisitsViewModel(IReadOnlyList<PortalVisitItemViewModel> Items);

public sealed record PortalVisitItemViewModel(
    Guid VisitId, Guid CatId, string CatName, DateOnly VisitedOn, string ServiceName, string VetName, bool HasDischargeSummary,
    DateOnly? FollowUpOn);

// ── Prescriptions (counts and dates only, ADR-262) ──────────────────────────────────────────────────
public sealed record PortalPrescriptionsViewModel(IReadOnlyList<PortalPrescriptionCatViewModel> ByCat);

public sealed record PortalPrescriptionCatViewModel(Guid CatId, string CatName, int ActiveCount, DateOnly? EarliestEndsOn);

// ── Orders ──────────────────────────────────────────────────────────────────────────────────────────
public sealed record PortalOrdersViewModel(IReadOnlyList<PortalOrderItemViewModel> Items);

public sealed record PortalOrderItemViewModel(
    Guid OrderId, string OrderNumber, DateTimeOffset PlacedOn, string Status, PortalMoneyViewModel Total, int ItemCount, bool CanReorder,
    string? TrackingNumber);

// ── Balances (guidance only; no payment action exists, ADR-264) ─────────────────────────────────────
public sealed record PortalMoneyViewModel(string Amount, string CurrencyCode);

public sealed record PortalBalancesViewModel(
    bool HasOutstanding, IReadOnlyList<PortalMoneyViewModel> Outstanding, IReadOnlyList<PortalMoneyViewModel> Overdue,
    int OpenInvoiceCount, DateOnly? OldestDueOn, IReadOnlyList<PortalInvoiceItemViewModel> Invoices, string HowToPay);

public sealed record PortalInvoiceItemViewModel(
    Guid InvoiceId, string? InvoiceNumber, DateOnly? IssuedOn, DateOnly? DueOn, PortalMoneyViewModel Total,
    PortalMoneyViewModel BalanceDue, string Status);
