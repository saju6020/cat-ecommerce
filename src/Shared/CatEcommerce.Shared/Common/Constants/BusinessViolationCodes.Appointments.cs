namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-APT-001 §10.4 -- Clinic Appointments' own violation codes. See ADR-341 for the
/// per-area partial pattern.</summary>
public static partial class BusinessViolationCodes
{
    public const string ServiceNotBookable = nameof(ServiceNotBookable);
    public const string SlotNoLongerAvailable = nameof(SlotNoLongerAvailable);
    public const string SlotOutsideAvailability = nameof(SlotOutsideAvailability);
    public const string SlotNotOnGrid = nameof(SlotNotOnGrid);
    public const string SlotTooSoon = nameof(SlotTooSoon);
    public const string SlotBeyondHorizon = nameof(SlotBeyondHorizon);
    public const string SlotHoldExpired = nameof(SlotHoldExpired);
    public const string HoldNotFound = nameof(HoldNotFound);
    public const string AppointmentNotFound = nameof(AppointmentNotFound);
    public const string InvalidAppointmentTransition = nameof(InvalidAppointmentTransition);
    public const string ChangeCutoffPassed = nameof(ChangeCutoffPassed);
    public const string NoShowTooEarly = nameof(NoShowTooEarly);
    public const string CheckInNotToday = nameof(CheckInNotToday);
    public const string PerformerNotQualified = nameof(PerformerNotQualified);
    public const string PerformerNotAvailable = nameof(PerformerNotAvailable);
    public const string PerformerBusy = nameof(PerformerBusy);
    public const string RoomKindMismatch = nameof(RoomKindMismatch);
    public const string RoomNotAvailable = nameof(RoomNotAvailable);
    public const string RoomBusy = nameof(RoomBusy);
    public const string CatAlreadyBooked = nameof(CatAlreadyBooked);
    public const string CatNotBookable = nameof(CatNotBookable);
    public const string SlotsStillAvailable = nameof(SlotsStillAvailable);
    public const string AlreadyWaitlisted = nameof(AlreadyWaitlisted);
    public const string WaitlistEntryNotFound = nameof(WaitlistEntryNotFound);
    public const string CancellationReasonRequired = nameof(CancellationReasonRequired);

    /// <summary>C7 move -- the caller's <c>ExpectedVersion</c> no longer matches the appointment's
    /// current version (gate fix: checked in the handler before <c>Reschedule</c> mutates it, since
    /// the aggregate's own <c>UpdateVersion()</c> would otherwise make the platform's post-mutation
    /// version check impossible to satisfy). Maps to <c>409</c>, the same shape a
    /// <c>DbUpdateConcurrencyException</c> would have produced.</summary>
    public const string StaleCalendarVersion = nameof(StaleCalendarVersion);
}
