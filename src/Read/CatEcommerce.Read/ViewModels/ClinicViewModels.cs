namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-APT-001 §12.4/§12.6 -- JSON camelCase, instants ISO-8601 with offset, money as
/// HO-ADM-001 §12.4. Every appointment view carries <c>branchTimeZoneId</c>.</summary>
public sealed record ServiceSnapshotViewModel(string Name, string Category, int DurationMinutes, decimal PriceAmount, string PriceCurrencyCode);

public sealed record SlotPerformerViewModel(Guid Id, string Name);

public sealed record SlotGroupViewModel(DateTimeOffset StartsOn, DateTimeOffset EndsOn, IReadOnlyList<SlotPerformerViewModel> Performers);

public sealed record AvailableSlotsViewModel(DateOnly Date, string BranchTimeZoneId, IReadOnlyList<SlotGroupViewModel> Slots);

public sealed record StaffSlotViewModel(DateTimeOffset StartsOn, DateTimeOffset EndsOn, Guid PerformerId, string PerformerName, Guid RoomId, string RoomName, bool OutsideOpeningHours);

public sealed record BookingPerformerViewModel(Guid Id, string Name);

public sealed record BranchRefViewModel(Guid Id, string Name, string? PhoneNumber);

public sealed record AppointmentStatusChangeViewModel(string From, string To, DateTimeOffset ChangedOn, Guid ChangedBy);

public sealed record AppointmentSummaryViewModel(
    Guid Id, Guid CatId, string CatName, ServiceSnapshotViewModel Service, DateTimeOffset? StartsOn, DateTimeOffset? EndsOn,
    string? PerformerName, BranchRefViewModel Branch, string BranchTimeZoneId, string Status, bool CanChange, DateTimeOffset? ChangeCutoffOn);

public sealed record AppointmentDetailViewModel(
    Guid Id, Guid BranchId, string BranchTimeZoneId, Guid CustomerId, Guid CatId, string CatName, ServiceSnapshotViewModel Service,
    Guid? PerformerId, string? PerformerName, Guid? RoomId, string? RoomName, DateTimeOffset? StartsOn, DateTimeOffset? EndsOn,
    bool IsWalkIn, string Channel, string Status, DateTimeOffset? HoldExpiresOn, bool IsWaitlistOffer, string? ReasonForVisit,
    string TriagePriority, string? PresentingComplaint, int Version, bool NeedsRescheduling, string? RescheduleFlagReason,
    bool CanChange, DateTimeOffset? ChangeCutoffOn, IReadOnlyList<AppointmentStatusChangeViewModel> StatusHistory,
    IReadOnlyList<string> AllowedTransitions);

/// <summary>Backlog #26 -- the customer's own view of a status change: no staff account id.</summary>
public sealed record CustomerAppointmentStatusChangeViewModel(string From, string To, DateTimeOffset ChangedOn);

/// <summary>Backlog #26 -- the customer's own projection of an appointment (HO-APT-001 §12.4).
/// Deliberately has no <c>customerId</c>, room, triage priority, presenting complaint, walk-in flag,
/// channel, <c>version</c> (the staff move guard) or <c>allowedTransitions</c> (staff actions), and
/// its history carries no staff account ids. A customer changes an appointment through
/// <c>canChange</c>/<c>changeCutoffOn</c>, not through a version or a transition list.</summary>
public sealed record CustomerAppointmentDetailViewModel(
    Guid Id, Guid BranchId, string BranchTimeZoneId, Guid CatId, string CatName, ServiceSnapshotViewModel Service,
    Guid? PerformerId, string? PerformerName, DateTimeOffset? StartsOn, DateTimeOffset? EndsOn,
    string Status, DateTimeOffset? HoldExpiresOn, bool IsWaitlistOffer, string? ReasonForVisit,
    bool NeedsRescheduling, string? RescheduleFlagReason, bool CanChange, DateTimeOffset? ChangeCutoffOn,
    IReadOnlyList<CustomerAppointmentStatusChangeViewModel> StatusHistory);

public sealed record ClinicQueueEntryViewModel(
    Guid AppointmentId, int Position, string CatName, bool CatIsHighRisk, string CustomerName, string Service,
    string TriagePriority, bool IsWalkIn, DateTimeOffset DueOn, DateTimeOffset? CheckedInOn, string Status,
    string? PerformerName, int? EstimatedWaitMinutes);

public sealed record ClinicQueueViewModel(DateTimeOffset GeneratedOn, int PerformersOnShift, IReadOnlyList<ClinicQueueEntryViewModel> Entries);

public sealed record FlaggedAppointmentViewModel(
    Guid Id, Guid CatId, string CatName, DateTimeOffset StartsOn, string RescheduleFlagReason, Guid? RescheduleFlagSourceId);

public sealed record CustomerAppointmentSummaryViewModel(int NoShowCount, IReadOnlyList<AppointmentSummaryViewModel> Upcoming, DateTimeOffset? LastVisitOn);

public sealed record WaitlistEntryViewModel(
    Guid Id, Guid BranchId, Guid CatId, string CatName, Guid ServiceId, string ServiceName, DateOnly LocalDate,
    string Status, Guid? OfferedAppointmentId, DateTimeOffset? OfferExpiresOn, DateTimeOffset CreatedOn);

public sealed record CalendarAvailabilityViewModel(Guid StaffAccountId, DateTimeOffset Start, DateTimeOffset End, bool OutsideOpeningHours);

public sealed record CalendarClosureViewModel(DateOnly StartsOn, DateOnly EndsOn, string Kind, string Reason);

public sealed record CalendarAppointmentViewModel(
    Guid Id, int Version, Guid CatId, string CatName, Guid CustomerId, string CustomerName, string ServiceName,
    DateTimeOffset? StartsOn, DateTimeOffset? EndsOn, Guid? PerformerId, Guid? RoomId, string Status, bool IsWalkIn,
    bool NeedsRescheduling, bool CatIsHighRisk, Guid? SurgeryId = null, Guid? AssistantId = null);

public sealed record CalendarResourceViewModel(Guid Id, string Name);

public sealed record CalendarViewModel(
    int GridMinutes, IReadOnlyList<CalendarResourceViewModel> Resources, IReadOnlyList<CalendarAvailabilityViewModel> Availability,
    IReadOnlyList<CalendarClosureViewModel> Closures, IReadOnlyList<CalendarAppointmentViewModel> Appointments);
