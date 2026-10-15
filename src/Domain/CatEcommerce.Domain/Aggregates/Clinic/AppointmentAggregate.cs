using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Clinic;

/// <summary>
/// HO-APT-001 §8.2/§8.3 (ADR-067, ADR-068, ADR-070). A hold is a row in this same table, in status
/// <see cref="AppointmentStatus.Held"/> -- it never appears as a booking to anyone until
/// <see cref="Confirm"/> or <see cref="BookDirect"/>. Time occupancy (performer/room/cat, while
/// <see cref="Status"/> is Held/Booked/CheckedIn/InConsultation and not a walk-in) is enforced by
/// three PostgreSQL exclusion constraints (§12.1), never by an application lock (ADR-068).
/// </summary>
public sealed class AppointmentAggregate : AggregateRoot
{
    private readonly List<AppointmentStatusChange> _statusHistory = [];
    private readonly List<AppointmentStaffTime> _staffTime = [];

    /// <summary>HO-SRG-001 section 8.4 (ADR-158) -- set only by <see cref="BookForSurgery"/>. While set, every other transition method refuses with
    /// <c>AppointmentManagedBySurgery</c>: the appointment cannot diverge from its surgery.</summary>
    public Guid? SurgeryId { get; private set; }

    /// <summary>HO-SRG-001 section 8.4 -- the nurse or vet assisting a surgery; occupied through <see cref="StaffTime"/> (ADR-159).</summary>
    public Guid? AssistantId { get; private set; }

    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CatId { get; private set; }
    // Named ClinicServiceId, not ServiceId -- the platform's own Entity base class already
    // declares a ServiceId property (CS0108 hide warning otherwise; matches ServicePriceAggregate).
    public Guid ClinicServiceId { get; private set; }
    public ServiceSnapshot ServiceSnapshot { get; private set; } = default!;
    public Guid? PerformerId { get; private set; }
    public Guid? RoomId { get; private set; }
    public DateTimeOffset? StartsOn { get; private set; }
    public DateTimeOffset? EndsOn { get; private set; }
    public bool IsWalkIn { get; private set; }
    public BookingChannel Channel { get; private set; } = default!;
    public AppointmentStatus Status { get; private set; } = default!;
    public DateTimeOffset? HoldExpiresOn { get; private set; }
    public Guid? WaitlistEntryId { get; private set; }
    public string? ReasonForVisit { get; private set; }
    public TriagePriority TriagePriority { get; private set; } = TriagePriority.Routine;
    public string? PresentingComplaint { get; private set; }
    public DateTimeOffset? CheckedInOn { get; private set; }
    public DateTimeOffset? StartedOn { get; private set; }
    public DateTimeOffset? CompletedOn { get; private set; }
    public DateTimeOffset? CancelledOn { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public bool CancelledByCustomer { get; private set; }
    public DateTimeOffset? NoShowRecordedOn { get; private set; }
    public Guid? NoShowRecordedBy { get; private set; }
    public int RescheduleCount { get; private set; }

    /// <summary><see cref="StartsOn"/> in round-trip ("O") format -- changed by every reschedule or
    /// move that actually moves the start time (§8.9). The reminder guard compares this.</summary>
    public string? ScheduleStamp { get; private set; }

    public bool NeedsRescheduling { get; private set; }
    public RescheduleFlagReason? RescheduleFlagReason { get; private set; }
    public Guid? RescheduleFlagSourceId { get; private set; }

    public IReadOnlyList<AppointmentStatusChange> StatusHistory => _statusHistory;

    /// <summary>HO-SRG-001 section 8.4 (ADR-159) -- derived occupancy rows, at most two (performer, assistant).</summary>
    public IReadOnlyList<AppointmentStaffTime> StaffTime => _staffTime;

    private AppointmentAggregate() { }

    public AppointmentAggregate(Guid id) : base(id) { }

    /// <summary>§8.6 -- a hold is not a booking; no domain event.</summary>
    public void PlaceHold(SlotHoldDto dto)
    {
        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        CatId = dto.CatId;
        ClinicServiceId = dto.ServiceId;
        ServiceSnapshot = dto.ServiceSnapshot;
        PerformerId = dto.PerformerId;
        RoomId = dto.RoomId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        IsWalkIn = false;
        Channel = dto.Channel;
        Status = AppointmentStatus.Held;
        HoldExpiresOn = dto.HoldExpiresOn;
        WaitlistEntryId = dto.WaitlistEntryId;
        TriagePriority = TriagePriority.Routine;
        RescheduleCount = 0;
        ScheduleStamp = Stamp(dto.StartsOn);

        SetDefaultValue(dto.UserContext);
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>The handler checks <see cref="HoldExpiresOn"/> first and calls
    /// <see cref="ExpireHold"/> (saved as its own outcome, US-CLN-03 AC2) rather than this method
    /// turning itself into a failed save -- this is a defence-in-depth check only, for the narrow
    /// race between that check and this call.</summary>
    public void Confirm(AppointmentConfirmDto dto)
    {
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.Confirm);

        if (dto.Now >= HoldExpiresOn)
            throw new DomainRuleViolationException(BusinessViolationCodes.SlotHoldExpired, ClinicMessages.SlotHoldExpired, nameof(HoldExpiresOn));

        ValidateReason(dto.ReasonForVisit);

        Transition(AppointmentStatus.Booked, dto.Now, dto.UserContext.UserId);
        ReasonForVisit = dto.ReasonForVisit;

        AddDomainEvent(new AppointmentBookedEvent
        {
            AppointmentId = Id, BranchId = BranchId, CustomerId = CustomerId, CatId = CatId, ServiceId = ClinicServiceId,
            PerformerId = PerformerId, StartsOn = StartsOn!.Value, Channel = Channel,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>Goes through <see cref="Transition"/> so the release is audited in
    /// <see cref="StatusHistory"/> like every other status change (§8.3, U-A3).</summary>
    public void ReleaseHold(Platform.Infrastructure.Common.Security.UserContext userContext, DateTimeOffset now)
    {
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.ReleaseHold);
        Transition(AppointmentStatus.HoldReleased, now, userContext.UserId);
        SetDefaultValue(userContext);
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>Stale-hold release, ADR-070 -- called by any write that takes time, before the
    /// mutating save, for every hold overlapping the slot's performer/room/cat range. A live hold
    /// (<c>now &lt; HoldExpiresOn</c>) is refused -- a no-op, the same convention
    /// <see cref="WaitlistEntryAggregate.Claim"/>/<see cref="WaitlistEntryAggregate.Lapse"/> use for
    /// a precondition that does not hold, rather than the caller's own pre-check being the only
    /// thing standing between a live hold and an early expiry (gate fix, HIGH #5).</summary>
    public void ExpireHold(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Held || now < HoldExpiresOn) return;

        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.ExpireHold);
        // Expiry is the system's act, not a person's -- recorded against the empty id.
        Transition(AppointmentStatus.HoldExpired, now, Guid.Empty);
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>Staff booking and moves skip the hold (ADR-072); same slot rules validated by the
    /// handler via <c>SlotClaim</c> before this is called.</summary>
    public void BookDirect(DirectBookingDto dto)
    {
        ValidateReason(dto.ReasonForVisit);

        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        CatId = dto.CatId;
        ClinicServiceId = dto.ServiceId;
        ServiceSnapshot = dto.ServiceSnapshot;
        PerformerId = dto.PerformerId;
        RoomId = dto.RoomId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        IsWalkIn = false;
        Channel = BookingChannel.Reception;
        Status = AppointmentStatus.Booked;
        ReasonForVisit = dto.ReasonForVisit;
        TriagePriority = TriagePriority.Routine;
        RescheduleCount = 0;
        ScheduleStamp = Stamp(dto.StartsOn);

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new AppointmentBookedEvent
        {
            AppointmentId = Id, BranchId = BranchId, CustomerId = CustomerId, CatId = CatId, ServiceId = ClinicServiceId,
            PerformerId = PerformerId, StartsOn = StartsOn!.Value, Channel = Channel,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>Customer reschedule (C4) and staff move (C7) share this method (§11.4). The old
    /// time is released and the new one taken atomically -- one row, one update (US-CLN-04 AC1).</summary>
    public void Reschedule(AppointmentRescheduleDto dto)
    {
        EnsureNotManagedBySurgery();
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.Reschedule);

        if (!dto.IsStaff && dto.ChangeCutoffHours is { } cutoff && dto.Now > StartsOn!.Value.AddHours(-cutoff))
            throw new DomainRuleViolationException(BusinessViolationCodes.ChangeCutoffPassed, ClinicMessages.ChangeCutoffPassed, nameof(StartsOn));

        var previousStartsOn = StartsOn!.Value;
        var startChanged = previousStartsOn != dto.StartsOn;

        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        PerformerId = dto.PerformerId;
        RoomId = dto.RoomId;

        if (startChanged)
        {
            RescheduleCount++;
            ScheduleStamp = Stamp(dto.StartsOn);
            NeedsRescheduling = false;
            RescheduleFlagReason = null;
            RescheduleFlagSourceId = null;

            AddDomainEvent(new AppointmentRescheduledEvent
            {
                AppointmentId = Id, BranchId = BranchId, StartsOn = dto.StartsOn, PreviousStartsOn = previousStartsOn,
                CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
            });
        }

        SyncStaffTime();
        UpdateVersion();
    }

    public void Cancel(AppointmentCancelDto dto)
    {
        EnsureNotManagedBySurgery();
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.Cancel);

        if (dto.CancelledByCustomer && dto.ChangeCutoffHours is { } cutoff && dto.Now > StartsOn!.Value.AddHours(-cutoff))
            throw new DomainRuleViolationException(BusinessViolationCodes.ChangeCutoffPassed, ClinicMessages.ChangeCutoffPassed, nameof(StartsOn));

        if (dto.Reason is { Length: > AppointmentRules.MaxReasonLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired, "The cancellation reason is too long.", nameof(dto.Reason));

        Transition(AppointmentStatus.Cancelled, dto.Now, dto.UserContext.UserId);
        CancelledOn = dto.Now;
        CancelledBy = dto.CancelledBy;
        CancellationReason = dto.Reason;
        CancelledByCustomer = dto.CancelledByCustomer;
        NeedsRescheduling = false;
        RescheduleFlagReason = null;
        RescheduleFlagSourceId = null;

        AddDomainEvent(new AppointmentCancelledEvent
        {
            AppointmentId = Id, BranchId = BranchId, CancelledByCustomer = dto.CancelledByCustomer,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        SyncStaffTime();
        UpdateVersion();
    }

    public void CheckIn(AppointmentCheckInDto dto)
    {
        EnsureNotManagedBySurgery();
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.CheckIn);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(dto.TimeZoneId);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(dto.Now, timeZone).Date);
        var localStartDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(StartsOn!.Value, timeZone).Date);
        var earlyCutoff = StartsOn.Value.AddMinutes(-dto.EarlyCheckInMinutes);

        var sameDay = localToday == localStartDate;
        var earlyForTomorrow = localToday == localStartDate.AddDays(-1) && dto.Now >= earlyCutoff;

        if (!sameDay && !earlyForTomorrow)
            throw new DomainRuleViolationException(BusinessViolationCodes.CheckInNotToday, "This appointment cannot be checked in yet.", nameof(StartsOn));

        Transition(AppointmentStatus.CheckedIn, dto.Now, dto.UserContext.UserId);
        CheckedInOn = dto.Now;
        if (dto.TriagePriority is { } priority) TriagePriority = priority;

        AddDomainEvent(new AppointmentCheckedInEvent { AppointmentId = Id, BranchId = BranchId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>Creates the appointment directly as <see cref="AppointmentStatus.CheckedIn"/> --
    /// no hold, no time, no performer until <see cref="Start"/> (§8.3).</summary>
    public void RegisterWalkIn(WalkInRegisterDto dto)
    {
        if (dto.PresentingComplaint is { Length: > AppointmentRules.MaxReasonLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired, "The presenting complaint is too long.", nameof(dto.PresentingComplaint));

        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        CatId = dto.CatId;
        ClinicServiceId = dto.ServiceId;
        ServiceSnapshot = dto.ServiceSnapshot;
        IsWalkIn = true;
        Channel = BookingChannel.WalkIn;
        Status = AppointmentStatus.CheckedIn;
        CheckedInOn = dto.Now;
        TriagePriority = dto.TriagePriority;
        PresentingComplaint = dto.PresentingComplaint;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new WalkInRegisteredEvent { AppointmentId = Id, BranchId = BranchId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        SyncStaffTime();
        UpdateVersion();
    }

    public void SetTriagePriority(AppointmentTriageDto dto)
    {
        EnsureNotManagedBySurgery();
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.SetTriagePriority);

        TriagePriority = dto.Priority;

        AddDomainEvent(new TriagePriorityChangedEvent { AppointmentId = Id, Priority = dto.Priority, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>A walk-in gets its performer and room here; a booked appointment may reassign the
    /// room (§11.4 C11). No exclusion constraint applies to a walk-in (§8.4).</summary>
    public void Start(AppointmentStartDto dto)
    {
        EnsureNotManagedBySurgery();
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.Start);

        if (IsWalkIn && (dto.PerformerId is null || dto.RoomId is null))
            throw new DomainRuleViolationException(BusinessViolationCodes.PerformerNotQualified, "A walk-in needs a performer and a room to start.", nameof(dto.PerformerId));

        if (dto.PerformerId is not null) PerformerId = dto.PerformerId;
        if (dto.RoomId is not null) RoomId = dto.RoomId;

        Transition(AppointmentStatus.InConsultation, dto.Now, dto.UserContext.UserId);
        StartedOn = dto.Now;

        AddDomainEvent(new AppointmentStartedEvent
        {
            AppointmentId = Id, BranchId = BranchId, CatId = CatId, PerformerId = PerformerId!.Value,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        SyncStaffTime();
        UpdateVersion();
    }

    public void Complete(AppointmentCompleteDto dto)
    {
        EnsureNotManagedBySurgery();
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.Complete);

        Transition(AppointmentStatus.Completed, dto.Now, dto.UserContext.UserId);
        CompletedOn = dto.Now;

        AddDomainEvent(new AppointmentCompletedEvent
        {
            AppointmentId = Id, BranchId = BranchId, CustomerId = CustomerId, CatId = CatId,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        SyncStaffTime();
        UpdateVersion();
    }

    public void RecordNoShow(AppointmentNoShowDto dto)
    {
        EnsureNotManagedBySurgery();
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.RecordNoShow);

        if (dto.Now < StartsOn!.Value.AddMinutes(dto.GraceMinutes))
            throw new DomainRuleViolationException(BusinessViolationCodes.NoShowTooEarly, "It is too soon to record this appointment as a no-show.", nameof(StartsOn));

        Transition(AppointmentStatus.NoShow, dto.Now, dto.UserContext.UserId);
        NoShowRecordedOn = dto.Now;
        NoShowRecordedBy = dto.RecordedBy;

        AddDomainEvent(new AppointmentNoShowRecordedEvent
        {
            AppointmentId = Id, BranchId = BranchId, CustomerId = CustomerId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>§8.10/ADR-076. Idempotent -- already flagged with the same source is a no-op.
    /// Never cancels, never enqueues.</summary>
    public void FlagForReschedule(AppointmentRescheduleFlagDto dto)
    {
        if (Status != AppointmentStatus.Booked) return;
        if (NeedsRescheduling && RescheduleFlagReason == dto.Reason && RescheduleFlagSourceId == dto.SourceId) return;

        NeedsRescheduling = true;
        RescheduleFlagReason = dto.Reason;
        RescheduleFlagSourceId = dto.SourceId;

        AddDomainEvent(new AppointmentFlaggedForRescheduleEvent { AppointmentId = Id, Reason = dto.Reason, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void ClearRescheduleFlag(AppointmentRescheduleUnflagDto dto)
    {
        if (!NeedsRescheduling || RescheduleFlagSourceId != dto.SourceId) return;

        NeedsRescheduling = false;
        RescheduleFlagReason = null;
        RescheduleFlagSourceId = null;
        UpdateVersion();
    }

    /// <summary>HO-ACC-001 <c>CustomerMergedEvent</c> contract (§11.6) -- reassigns the appointment
    /// to the survivor. Idempotent by nature (setting the same id twice is harmless).</summary>
    public void ReassignOwner(AppointmentOwnerReassignDto dto)
    {
        if (CustomerId == dto.NewCustomerId) return;

        CustomerId = dto.NewCustomerId;
        UpdateVersion();
    }

    // -- HO-SRG-001 section 8.4 / 11.7 G1 (ADR-158, ADR-159) -- a surgery time is an appointment --

    /// <summary>Staff mode, no hold: the appointment that holds a surgery theatre, surgeon and assistant. Created in the same save as the surgery.
    /// Raises no appointment event -- the surgery raises its own.</summary>
    public void BookForSurgery(SurgeryAppointmentDto dto)
    {
        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        CatId = dto.CatId;
        ClinicServiceId = dto.ServiceId;
        ServiceSnapshot = dto.ServiceSnapshot;
        PerformerId = dto.SurgeonId;
        AssistantId = dto.AssistantId;
        RoomId = dto.TheatreId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        SurgeryId = dto.SurgeryId;
        IsWalkIn = false;
        Channel = BookingChannel.Surgery;
        Status = AppointmentStatus.Booked;
        TriagePriority = TriagePriority.Routine;
        RescheduleCount = 0;
        ScheduleStamp = Stamp(dto.StartsOn);

        SetDefaultValue(dto.UserContext);
        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>The surgery was rescheduled or re-staffed. Clears any needs-rescheduling flag: moving the surgery is the resolution.</summary>
    public void MoveForSurgery(SurgeryAppointmentMoveDto dto)
    {
        EnsureSurgeryAppointment(dto.SurgeryId);
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.Reschedule);

        var startChanged = StartsOn != dto.StartsOn;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        PerformerId = dto.SurgeonId;
        AssistantId = dto.AssistantId;
        RoomId = dto.TheatreId;

        if (startChanged)
        {
            RescheduleCount++;
            ScheduleStamp = Stamp(dto.StartsOn);
        }

        NeedsRescheduling = false;
        RescheduleFlagReason = null;
        RescheduleFlagSourceId = null;

        SyncStaffTime();
        UpdateVersion();
    }

    public void CancelForSurgery(SurgeryAppointmentCancelDto dto)
    {
        EnsureSurgeryAppointment(dto.SurgeryId);
        AppointmentTransitionRules.EnsureAllowed(Status, AppointmentTransitionRules.Cancel);

        Transition(AppointmentStatus.Cancelled, dto.Now, dto.UserContext.UserId);
        CancelledOn = dto.Now;
        CancelledBy = dto.UserContext.UserId;
        CancellationReason = dto.Reason;
        CancelledByCustomer = false;
        NeedsRescheduling = false;
        RescheduleFlagReason = null;
        RescheduleFlagSourceId = null;

        SyncStaffTime();
        UpdateVersion();
    }

    /// <summary>The procedure ended: Booked to Completed, which frees any remaining planned time (a Completed appointment no longer occupies).</summary>
    public void CompleteSurgerySlot(SurgeryAppointmentCompleteDto dto)
    {
        EnsureSurgeryAppointment(dto.SurgeryId);
        if (Status != AppointmentStatus.Booked)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.InvalidAppointmentTransition,
                $"A {Status.DisplayName.ToLowerInvariant()} appointment cannot be completed.", nameof(Status));

        Transition(AppointmentStatus.Completed, dto.Now, dto.UserContext.UserId);
        CompletedOn = dto.Now;

        SyncStaffTime();
        UpdateVersion();
    }

    private void EnsureNotManagedBySurgery()
    {
        if (SurgeryId is not null)
            throw new DomainRuleViolationException(BusinessViolationCodes.AppointmentManagedBySurgery, SurgeryMessages.AppointmentManagedBySurgery, nameof(SurgeryId));
    }

    private void EnsureSurgeryAppointment(Guid surgeryId)
    {
        if (SurgeryId != surgeryId)
            throw new DomainRuleViolationException(BusinessViolationCodes.AppointmentManagedBySurgery, "This appointment does not belong to that surgery.", nameof(SurgeryId));
    }

    /// <summary>ADR-159 -- keeps the owned staff-time rows equal to what the appointment occupies. Called at the end of EVERY method that changes status,
    /// time, performer or assistant (U-A8). A row is updated in place; one with no staff member or no time is removed.</summary>
    private void SyncStaffTime()
    {
        var occupies = !IsWalkIn && (Status == AppointmentStatus.Held || Status == AppointmentStatus.Booked
            || Status == AppointmentStatus.CheckedIn || Status == AppointmentStatus.InConsultation);

        SyncRole(StaffTimeRole.Performer, PerformerId, occupies);
        SyncRole(StaffTimeRole.Assistant, AssistantId, occupies);
    }

    private void SyncRole(StaffTimeRole role, Guid? staffAccountId, bool occupies)
    {
        var existing = _staffTime.FirstOrDefault(r => r.Role == role);

        if (staffAccountId is null || StartsOn is null || EndsOn is null)
        {
            if (existing is not null) _staffTime.Remove(existing);
            return;
        }

        if (existing is null)
            _staffTime.Add(new AppointmentStaffTime(Guid.NewGuid(), role, staffAccountId.Value, StartsOn.Value, EndsOn.Value, occupies));
        else
            existing.Update(staffAccountId.Value, StartsOn.Value, EndsOn.Value, occupies);
    }

    private void Transition(AppointmentStatus to, DateTimeOffset now, Guid changedBy)
    {
        // Never trims: Clinical/Forbidden history is not deletable (C-ALL-4). The state machine
        // bounds the length -- a booking passes through each status at most once.
        // Monotonic sequence, so ordering never ties (APT backlog row 47).
        var sequence = _statusHistory.Count == 0 ? 1 : _statusHistory.Max(h => h.Sequence) + 1;
        _statusHistory.Add(new AppointmentStatusChange(Guid.NewGuid(), Status, to, now, changedBy, sequence));
        Status = to;
    }

    private static void ValidateReason(string? reason)
    {
        if (reason is { Length: > AppointmentRules.MaxReasonLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired, "The reason for visit is too long.", nameof(reason));
    }

    private static string Stamp(DateTimeOffset startsOn) => startsOn.ToString("O");
}
