using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §11.4 -- the one sequence every handler that TAKES TIME (book C2, reschedule C3) follows, counterpart of HO-APT-001's <c>SlotClaim</c>: load the inputs, judge
/// the requested start with the same pure rules <see cref="SurgerySlotFinder"/> uses (staff mode), and on any clash return EVERY reason; then expire the stale holds that
/// would otherwise 23P01 a slot the caller was just told is free. A static helper over the ports -- not a service layer (ADR-015). The exclusion constraints, not this
/// helper, are the control against a race (ADR-068).
/// </summary>
public static class SurgerySlotClaim
{
    public sealed record Request(
        Guid BranchId, Guid ServiceId, DateTimeOffset StartsOn, Guid SurgeonId, Guid AssistantId, Guid TheatreId, Guid CatId, Guid? ExcludeAppointmentId,
        DateTimeOffset Now, int? DurationOverrideMinutes = null);

    /// <summary><paramref name="Error"/> is a refusal that is not a clash (service not surgical or not bookable); <paramref name="Clashes"/> is empty when the start is free.</summary>
    public sealed record Outcome(
        SurgeryInputSet? Inputs, string TimeZoneId, DateOnly LocalDate, int DurationMinutes, SurgeryClash? Error, IReadOnlyList<SurgeryClash> Clashes)
    {
        public bool IsSuccess => Error is null && Clashes.Count == 0;
    }

    public static async Task<Outcome> ValidateAsync(
        Request request, string timeZoneId, ISurgerySchedulingInputs schedulingInputs, IBusinessSettings settings, CancellationToken ct = default)
    {
        var localDate = BranchCalendar.LocalDateOf(request.StartsOn, timeZoneId);
        var inputs = await schedulingInputs.LoadAsync(new SurgeryInputRequest(
            request.BranchId, request.ServiceId, localDate, request.SurgeonId, request.AssistantId, request.TheatreId, request.CatId, request.ExcludeAppointmentId), ct);

        var service = inputs.Service;
        if (service is null || !service.IsActive || service.Price is null)
            return new Outcome(inputs, timeZoneId, localDate, 0, new SurgeryClash(BusinessViolationCodes.ServiceNotBookable, "serviceId", ClinicMessages.ServiceNotBookable), []);
        if (service.RequiredRoomKind != RoomKind.Theatre)
            return new Outcome(inputs, timeZoneId, localDate, 0, new SurgeryClash(BusinessViolationCodes.ServiceNotSurgical, "serviceId", SurgeryMessages.ServiceNotSurgical), []);

        var duration = request.DurationOverrideMinutes ?? service.DurationMinutes;
        var slotRequest = new SurgerySlotRequest(
            new SurgeryProcedureInfo(duration, service.QualifiedRoleIds), timeZoneId, inputs.Surgeon, inputs.Assistant, inputs.Theatre,
            inputs.SurgeonBusy, inputs.AssistantBusy, inputs.TheatreBusy, inputs.CatBusy,
            settings.GetInt(SettingKeys.BookingSlotIntervalMinutes, request.BranchId), request.Now);

        return new Outcome(inputs, timeZoneId, localDate, duration, null, SurgerySlotFinder.Explain(slotRequest, request.StartsOn));
    }

    /// <summary>
    /// HO-APT-001 ADR-070 (as amended 2026-09-28): a lapsed hold still matches the exclusion predicate until it is expired, so every write that claims a slot first expires the
    /// stale holds overlapping it -- for the surgeon and theatre, then for the ASSISTANT (the assistant is protected by the staff-time constraint, which also sees a lapsed
    /// hold's row). Each expiry commits on its own, so no caller may have pending tracked changes: call this BEFORE mutating anything.
    /// </summary>
    public static async Task ExpireStaleHoldsAsync(
        IStaleHoldReleaser staleHoldReleaser, IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
        Guid surgeonId, Guid assistantId, Guid theatreId, Guid catId, DateTimeOffset startsOn, DateTimeOffset endsOn, DateTimeOffset now, Guid? excludeAppointmentId)
    {
        foreach (var staffId in new[] { surgeonId, assistantId })
        {
            var slot = new Slot(startsOn, endsOn, staffId, "", theatreId, "", false);
            await SlotClaim.ExpireOverlappingStaleHoldsAsync(staleHoldReleaser, appointmentRepository, slot, catId, now, excludeAppointmentId);
        }
    }

    /// <summary>The clashes as the 400's violations: one per clash, each with its own <c>propertyName</c>.</summary>
    public static IEnumerable<(string Code, string Property, string Message)> AsViolations(IEnumerable<SurgeryClash> clashes) =>
        clashes.Select(c => (c.Code, c.Property, c.Message));
}
