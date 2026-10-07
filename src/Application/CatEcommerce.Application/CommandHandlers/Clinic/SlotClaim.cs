using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Clinic;

/// <summary>
/// HO-APT-001 §11.4 -- the one sequence every handler that takes time follows (C1, C4, C6, C7,
/// C15-offer): load inputs, validate the requested slot with the same rules
/// <see cref="AppointmentSlotFinder"/> uses (online or staff mode), on failure the specific code.
/// A static helper over the ports -- not a service layer (ADR-015 still applies: this holds no
/// state and makes no decision a handler does not ask it to).
/// </summary>
public static class SlotClaim
{
    public sealed record Request(
        Guid BranchId, Guid ServiceId, DateTimeOffset StartsOn, Guid? PerformerId, Guid? CatId,
        Guid? ExcludeAppointmentId, bool IsStaff, DateTimeOffset Now, Guid? RoomId = null);

    public sealed record Outcome(
        AppointmentSchedulingInputSet? Inputs, Slot? Slot, string? TimeZoneId, DateOnly LocalDate,
        string? ErrorCode, string? Message)
    {
        public bool IsSuccess => ErrorCode is null;
    }

    public static async Task<Outcome> ValidateAsync(
        Request request,
        IAggregateRootRepository<BranchAggregate> branchRepository,
        IAppointmentSchedulingInputs schedulingInputs,
        IBusinessSettings businessSettings,
        CancellationToken ct = default)
    {
        var branch = await branchRepository.GetByIdAsync(request.BranchId);
        if (branch is null || !branch.IsActive)
            return Fail(BusinessViolationCodes.ServiceNotBookable, "Branch not found or inactive.");

        var localDate = BranchCalendar.LocalDateOf(request.StartsOn, branch.TimeZoneId);

        var inputSet = await schedulingInputs.LoadAsync(new AppointmentSchedulingInputRequest(
            request.BranchId, request.ServiceId, localDate, RestrictToOpeningHours: !request.IsStaff,
            request.ExcludeAppointmentId, request.CatId, request.PerformerId), ct);

        var service = inputSet.Service;
        if (service is null || !service.IsActive || service.Price is null || (!request.IsStaff && !service.IsBookableOnline))
            return Fail(BusinessViolationCodes.ServiceNotBookable, ClinicMessages.ServiceNotBookable);

        var gridMinutes = businessSettings.GetInt(SettingKeys.BookingSlotIntervalMinutes, request.BranchId);
        if (!AppointmentSlotFinder.GridStartsForDay(localDate, gridMinutes, branch.TimeZoneId).Contains(request.StartsOn))
            return Fail(BusinessViolationCodes.SlotNotOnGrid, "This time is not on the booking grid.");

        var earliestStart = request.Now;
        if (!request.IsStaff)
        {
            var minimumNoticeMinutes = businessSettings.GetInt(SettingKeys.BookingMinimumNoticeMinutes, request.BranchId);
            earliestStart = request.Now.AddMinutes(minimumNoticeMinutes);
            if (request.StartsOn < earliestStart)
                return Fail(BusinessViolationCodes.SlotTooSoon, "This slot is too soon to book online.");

            var horizonDays = businessSettings.GetInt(SettingKeys.BookingHorizonDays, request.BranchId);
            var today = BranchCalendar.LocalDateOf(request.Now, branch.TimeZoneId);
            if (localDate > today.AddDays(horizonDays))
                return Fail(BusinessViolationCodes.SlotBeyondHorizon, "This date is beyond the online booking horizon.");
        }

        if (request.PerformerId is { } requestedPerformerId)
        {
            var performer = inputSet.Performers.FirstOrDefault(p => p.StaffAccountId == requestedPerformerId);
            if (performer is null || !performer.RoleIds.Any(r => service.QualifiedRoleIds.Contains(r, StringComparer.Ordinal)))
                return Fail(BusinessViolationCodes.PerformerNotQualified, "This performer is not qualified for this service.");
        }

        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        var endsOn = request.StartsOn + duration;

        var slotRequest = new SlotRequest(
            new SlotServiceInfo(service.DurationMinutes, service.QualifiedRoleIds, service.RequiredRoomKind),
            localDate, branch.TimeZoneId, inputSet.Performers, inputSet.Rooms, inputSet.Busy,
            gridMinutes, earliestStart, request.PerformerId, request.RoomId);

        var match = AppointmentSlotFinder.FindSlots(slotRequest)
            .FirstOrDefault(s => s.StartsOn == request.StartsOn && (request.PerformerId is null || s.PerformerId == request.PerformerId));

        if (match is not null)
            return new Outcome(inputSet, match, branch.TimeZoneId, localDate, null, null);

        return Diagnose(request, inputSet, service, endsOn, branch.TimeZoneId);
    }

    /// <summary>The requested slot did not come out of the finder -- work out the specific reason
    /// (§10.4/§12.1) rather than a generic refusal.
    ///
    /// ACCEPTANCE gate fix (2026-09-28, A14/A15): §10.4 requires <c>PerformerBusy</c>,
    /// <c>RoomBusy</c> and <c>CatAlreadyBooked</c> to name the conflicting appointment's local time
    /// (<em>"Dr Rahman already has an appointment 10:00-10:30."</em>) -- the previous messages
    /// ("This performer already has an appointment at this time.") never did. Finds the specific
    /// overlapping interval and formats it in the branch's own local time
    /// (<c>TimeZoneInfo.ConvertTime</c>, the same instant-to-local-display conversion
    /// <c>ClinicNotifications.FormatLocal</c> already uses for message variables -- ADR-045 governs
    /// local-to-<em>instant</em> conversion, not this read-only display direction).</summary>
    private static Outcome Diagnose(Request request, AppointmentSchedulingInputSet inputSet, SchedulingServiceInfo service, DateTimeOffset endsOn, string timeZoneId)
    {
        if (request.PerformerId is { } performerId)
        {
            var performer = inputSet.Performers.FirstOrDefault(p => p.StaffAccountId == performerId);
            if (performer is not null && !performer.AvailableIntervals.Any(w => w.Start <= request.StartsOn && endsOn <= w.End))
                return Fail(BusinessViolationCodes.PerformerNotAvailable, "This performer is not available at this time.");

            if (performer is not null)
            {
                var conflict = FindOverlap(inputSet.Busy.ForPerformer(performerId), request.StartsOn, endsOn);
                if (conflict is not null)
                    return Fail(BusinessViolationCodes.PerformerBusy, $"{performer.Name} already has an appointment {FormatLocalRange(conflict, timeZoneId)}.");
            }
        }

        var catConflict = FindOverlap(inputSet.Busy.CatBusyIntervals, request.StartsOn, endsOn);
        if (catConflict is not null)
            return Fail(BusinessViolationCodes.CatAlreadyBooked, $"This cat already has an appointment {FormatLocalRange(catConflict, timeZoneId)}.");

        var roomsOfKind = inputSet.Rooms.Where(r => r.IsActive && r.Kind == service.RequiredRoomKind)
            .Where(r => request.RoomId is null || r.RoomId == request.RoomId)
            .ToList();
        if (roomsOfKind.Count == 0)
            return Fail(BusinessViolationCodes.RoomKindMismatch, "No active room of the required kind exists at this branch.");

        if (roomsOfKind.All(r => Overlaps(inputSet.Busy.ForRoom(r.RoomId), request.StartsOn, endsOn)))
        {
            // Name the first busy room and its conflicting time -- deterministic and, for the
            // common single-room-of-a-kind branch, exact; with several candidate rooms there is no
            // one "the" conflict to name, so this still names *a* real conflict rather than guess.
            var busyRoom = roomsOfKind[0];
            var roomConflict = FindOverlap(inputSet.Busy.ForRoom(busyRoom.RoomId), request.StartsOn, endsOn);
            var message = roomConflict is not null
                ? $"{busyRoom.Name} already has an appointment {FormatLocalRange(roomConflict, timeZoneId)}."
                : "No room of the required kind is free at this time.";
            return Fail(BusinessViolationCodes.RoomBusy, message);
        }

        return Fail(BusinessViolationCodes.SlotNoLongerAvailable, ClinicMessages.SlotNoLongerAvailable);
    }

    private static bool Overlaps(IReadOnlyList<TimeInterval> intervals, DateTimeOffset start, DateTimeOffset end) =>
        intervals.Any(i => i.Start < end && start < i.End);

    private static TimeInterval? FindOverlap(IReadOnlyList<TimeInterval> intervals, DateTimeOffset start, DateTimeOffset end) =>
        intervals.FirstOrDefault(i => i.Start < end && start < i.End);

    private static string FormatLocalRange(TimeInterval interval, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var start = TimeZoneInfo.ConvertTime(interval.Start, timeZone);
        var end = TimeZoneInfo.ConvertTime(interval.End, timeZone);
        return $"{start:HH:mm}–{end:HH:mm}";
    }

    /// <summary>§8.4/ADR-070/§11.4 step 3 -- every write that claims a slot first expires the stale
    /// holds overlapping it: a <c>Held</c> row whose expiry has passed still matches the
    /// exclusion-constraint predicate, so a lapsed hold would otherwise 23P01 a slot the caller was
    /// just told is free. Every handler that calls <see cref="ValidateAsync"/> with a successful
    /// outcome calls this next, before mutating -- C1, C4, C6, C7 and C15-offer.
    ///
    /// <b>ADR-070's 2026-09-28 amendment (owner decision).</b> Each stale hold is saved here, in its
    /// own <c>UpdateAsync</c> -- its own <c>SaveChangesAsync</c> -- rather than left tracked for the
    /// caller's own later save to pick up, contrary to the ADR's original "in the same save"
    /// wording. Root-caused with EF's own SQL logging: when the caller's own mutation is itself an
    /// <em>update</em> to another row of the same table (C4 reschedule, C7 move),
    /// <c>SaveChangesAsync</c> does not order two <c>Modified</c> entries of the same entity type by
    /// which was mutated first in C# -- it sent the reschedule's UPDATE before the expiry's UPDATE
    /// often enough to make I-D3/I-F2-style tests flaky. Postgres checks a (non-deferrable)
    /// exclusion constraint per statement, so the reschedule's UPDATE still saw the stale hold as
    /// <c>Held</c> and 23P01'd. Committing the expiry first removes the ordering dependency
    /// entirely; expiring a hold that has, in fact, lapsed is correct and idempotent regardless of
    /// what the caller's own claim does next.
    ///
    /// <b>Rule this trades for that atomicity, and enforces:</b> no caller may have pending tracked
    /// changes on its <c>DbContext</c> before calling this helper. A caller that mutated first would
    /// have half its own unit of work committed early by this helper's own save -- checked via
    /// <see cref="IStaleHoldReleaser.HasPendingChanges"/> so a future caller that breaks the rule
    /// fails loudly instead of silently committing a partial write.</summary>
    public static async Task ExpireOverlappingStaleHoldsAsync(
        IStaleHoldReleaser staleHoldReleaser,
        IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
        Slot slot, Guid catId, DateTimeOffset now, Guid? excludeAppointmentId,
        CancellationToken ct = default)
    {
        if (staleHoldReleaser.HasPendingChanges())
        {
            throw new InvalidOperationException(
                "SlotClaim.ExpireOverlappingStaleHoldsAsync requires no pending tracked changes on " +
                "entry (ADR-070's 2026-09-28 amendment): it commits each stale hold's expiry in its " +
                "own save, so a caller with pending changes of its own would have half its unit of " +
                "work committed early by this helper's save. Call this before mutating anything.");
        }

        var staleIds = await staleHoldReleaser.FindStaleOverlappingHoldIdsAsync(
            slot.StartsOn, slot.EndsOn, slot.PerformerId, slot.RoomId, catId, now, excludeAppointmentId, ct);

        foreach (var staleId in staleIds)
        {
            var stale = await appointmentRepository.GetByIdAsync(staleId);
            if (stale is null || stale.Status != AppointmentStatus.Held) continue;

            stale.ExpireHold(now);

            // Backlog #31: losing the race for this hold's row to another request is not an error
            // here -- that request already settled the hold; move on to the next one.
            await staleHoldReleaser.TryCommitExpiryAsync(stale,
                () => appointmentRepository.UpdateAsync(stale, EventPublishOption.InMemoryPublishOnly));
        }
    }

    private static Outcome Fail(string errorCode, string message) => new(null, null, null, default, errorCode, message);

    private static readonly HashSet<string> OccupationCodes = new(StringComparer.Ordinal)
    {
        BusinessViolationCodes.PerformerNotAvailable, BusinessViolationCodes.PerformerBusy,
        BusinessViolationCodes.RoomNotAvailable, BusinessViolationCodes.RoomBusy,
        BusinessViolationCodes.RoomKindMismatch, BusinessViolationCodes.CatAlreadyBooked,
    };

    /// <summary>§12.5 -- a customer never needs to know which resource lost the race; every
    /// "the slot is occupied" reason collapses to the one 409 code. Input-shape refusals
    /// (<c>ServiceNotBookable</c>, <c>SlotNotOnGrid</c>, <c>SlotTooSoon</c>, <c>SlotBeyondHorizon</c>,
    /// <c>PerformerNotQualified</c>) are left as themselves, at 400.</summary>
    public static Outcome ForCustomer(Outcome outcome) =>
        outcome.ErrorCode is { } code && OccupationCodes.Contains(code)
            ? Fail(BusinessViolationCodes.SlotNoLongerAvailable, ClinicMessages.SlotNoLongerAvailable)
            : outcome;
}
