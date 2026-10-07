using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-APT-001 §11.3 -- inputs to <c>AppointmentAggregate</c>'s mutation methods (§8.3).</summary>
public sealed record SlotHoldDto(
    Guid BranchId, Guid CustomerId, Guid CatId, Guid ServiceId, ServiceSnapshot ServiceSnapshot,
    Guid? PerformerId, Guid? RoomId, DateTimeOffset StartsOn, DateTimeOffset EndsOn,
    BookingChannel Channel, Guid? WaitlistEntryId, DateTimeOffset HoldExpiresOn,
    UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentConfirmDto(DateTimeOffset Now, string? ReasonForVisit, UserContext UserContext, Guid CorrelationId);

public sealed record DirectBookingDto(
    Guid BranchId, Guid CustomerId, Guid CatId, Guid ServiceId, ServiceSnapshot ServiceSnapshot,
    Guid PerformerId, Guid RoomId, DateTimeOffset StartsOn, DateTimeOffset EndsOn,
    string? ReasonForVisit, UserContext UserContext, Guid CorrelationId);

/// <summary>Used by both the customer's reschedule (C4, <paramref name="ChangeCutoffHours"/> set,
/// <paramref name="IsStaff"/> false) and staff's move (C7, <paramref name="IsStaff"/> true -- the
/// cut-off is never checked). The aggregate only regenerates <c>ScheduleStamp</c> (and so only
/// counts as a reschedule for messaging purposes) when <paramref name="StartsOn"/> actually
/// changes -- a room-only move does not (U-A7).</summary>
public sealed record AppointmentRescheduleDto(
    DateTimeOffset StartsOn, DateTimeOffset EndsOn, Guid PerformerId, Guid RoomId,
    DateTimeOffset Now, int? ChangeCutoffHours, bool IsStaff, UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentCancelDto(
    Guid CancelledBy, bool CancelledByCustomer, string? Reason, DateTimeOffset Now, int? ChangeCutoffHours,
    UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentCheckInDto(
    DateTimeOffset Now, string TimeZoneId, int EarlyCheckInMinutes, TriagePriority? TriagePriority,
    UserContext UserContext, Guid CorrelationId);

public sealed record WalkInRegisterDto(
    Guid BranchId, Guid CustomerId, Guid CatId, Guid ServiceId, ServiceSnapshot ServiceSnapshot,
    TriagePriority TriagePriority, string? PresentingComplaint, DateTimeOffset Now,
    UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentTriageDto(TriagePriority Priority, UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentStartDto(Guid? PerformerId, Guid? RoomId, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentCompleteDto(DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentNoShowDto(Guid RecordedBy, DateTimeOffset Now, int GraceMinutes, UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentRescheduleFlagDto(RescheduleFlagReason Reason, Guid SourceId, UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentRescheduleUnflagDto(Guid SourceId, UserContext UserContext, Guid CorrelationId);

public sealed record AppointmentOwnerReassignDto(Guid NewCustomerId, UserContext UserContext, Guid CorrelationId);
