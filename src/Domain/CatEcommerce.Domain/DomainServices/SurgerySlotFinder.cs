using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SRG-001 §8.5 (US-SRG-01, ADR-158) -- pure, no I/O: which starts are free for a theatre, a surgeon AND an assistant, and, for any one start, EVERY reason it is
/// refused (so the booking answers with one violation per clash, not the first). Busy intervals come from the port; this class never reads a database.
/// The database's exclusion constraints, not this class, are the control against a race (ADR-068) -- this class gives the specific, early, readable reason.
/// </summary>
public static class SurgerySlotFinder
{
    public static IReadOnlyList<SurgerySlot> FindStarts(SurgerySlotRequest request, DateOnly localDate)
    {
        if (request.Surgeon is null || request.Assistant is null || request.Theatre is null) return [];

        var slots = new List<SurgerySlot>();
        foreach (var start in AppointmentSlotFinder.GridStartsForDay(localDate, request.GridMinutes, request.TimeZoneId))
        {
            if (Explain(request, start).Count > 0) continue;

            var end = start.AddMinutes(request.Procedure.DurationMinutes);
            slots.Add(new SurgerySlot(start, end, OutsideOpeningHours(request.Surgeon, start, end) || OutsideOpeningHours(request.Assistant, start, end)));
        }

        return slots;
    }

    /// <summary>Every clash of <paramref name="start"/>, in the fixed order of §8.5. Empty means the start is free.</summary>
    public static IReadOnlyList<SurgeryClash> Explain(SurgerySlotRequest request, DateTimeOffset start)
    {
        var clashes = new List<SurgeryClash>();
        var end = start.AddMinutes(request.Procedure.DurationMinutes);
        var surgeon = request.Surgeon;
        var assistant = request.Assistant;

        if (surgeon is not null && !surgeon.RoleIds.Any(r => request.Procedure.QualifiedRoleIds.Contains(r, StringComparer.Ordinal)))
            clashes.Add(new SurgeryClash(BusinessViolationCodes.SurgeonNotQualified, "surgeonId", $"{surgeon.Name} is not qualified to perform this procedure."));

        if (assistant is not null && !assistant.RoleIds.Any(r => SurgeryRules.AssistantRoleIds.Contains(r, StringComparer.Ordinal)))
            clashes.Add(new SurgeryClash(BusinessViolationCodes.AssistantNotQualified, "assistantId", $"{assistant.Name} cannot assist in theatre. An assistant must be a nurse or a vet."));

        if (surgeon is not null && assistant is not null && surgeon.StaffAccountId == assistant.StaffAccountId)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.AssistantSameAsSurgeon, "assistantId", "The assistant must be a different person from the surgeon."));

        var theatre = request.Theatre;
        if (theatre is null || !theatre.IsActive || theatre.Kind != RoomKind.Theatre)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.TheatreNotAvailable, "theatreId", "That room is not an active theatre at this branch."));

        if (surgeon is null)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.SurgeonNotAvailable, "surgeonId", "The surgeon is not rostered at this branch."));
        else if (!IsWithinAvailability(surgeon, start, end))
            clashes.Add(new SurgeryClash(BusinessViolationCodes.SurgeonNotAvailable, "surgeonId", $"{surgeon.Name} is not available {Range(start, end, request.TimeZoneId)}."));

        if (assistant is null)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.AssistantNotAvailable, "assistantId", "The assistant is not rostered at this branch."));
        else if (!IsWithinAvailability(assistant, start, end))
            clashes.Add(new SurgeryClash(BusinessViolationCodes.AssistantNotAvailable, "assistantId", $"{assistant.Name} is not available {Range(start, end, request.TimeZoneId)}."));

        if (!AppointmentSlotFinder.GridStartsForDay(BranchCalendar.LocalDateOf(start, request.TimeZoneId), request.GridMinutes, request.TimeZoneId).Contains(start))
            clashes.Add(new SurgeryClash(BusinessViolationCodes.SlotNotOnGrid, "startsOn", "This time is not on the booking grid."));

        if (start < request.EarliestStart)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.SlotInPast, "startsOn", "This time has already passed."));

        if (Overlap(request.TheatreBusy, start, end) is { } theatreBusy)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.TheatreBusy, "theatreId", $"{theatre?.Name ?? "The theatre"} is already booked {Range(theatreBusy.Start, theatreBusy.End, request.TimeZoneId)}."));

        if (Overlap(request.SurgeonBusy, start, end) is { } surgeonBusy)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.SurgeonBusy, "surgeonId", $"{surgeon?.Name ?? "The surgeon"} {Describe(surgeonBusy.Kind)} {Range(surgeonBusy.Start, surgeonBusy.End, request.TimeZoneId)}."));

        if (Overlap(request.AssistantBusy, start, end) is { } assistantBusy)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.AssistantBusy, "assistantId", $"{assistant?.Name ?? "The assistant"} {Describe(assistantBusy.Kind)} {Range(assistantBusy.Start, assistantBusy.End, request.TimeZoneId)}."));

        if (Overlap(request.CatBusy, start, end) is { } catBusy)
            clashes.Add(new SurgeryClash(BusinessViolationCodes.CatAlreadyBooked, "catId", $"This cat already has an appointment {Range(catBusy.Start, catBusy.End, request.TimeZoneId)}."));

        return clashes;
    }

    /// <summary>The busy interval's local time range as the clash messages print it (<c>10:00-11:00</c>), in the branch zone.</summary>
    public static string Range(DateTimeOffset start, DateTimeOffset end, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var from = TimeZoneInfo.ConvertTime(start, zone);
        var to = TimeZoneInfo.ConvertTime(end, zone);
        return string.Create(CultureInfo.InvariantCulture, $"{from:HH:mm}–{to:HH:mm}");
    }

    private static string Describe(SurgeryBusyKind kind) => kind switch
    {
        SurgeryBusyKind.AsAssistant => "is assisting another surgery",
        SurgeryBusyKind.AsSurgeon => "is operating on another surgery",
        _ => "already has an appointment",
    };

    private static SurgeryBusyInterval? Overlap(IReadOnlyList<SurgeryBusyInterval> intervals, DateTimeOffset start, DateTimeOffset end) =>
        intervals.FirstOrDefault(i => i.Start < end && start < i.End);

    private static bool IsWithinAvailability(SlotPerformerInput staff, DateTimeOffset start, DateTimeOffset end) =>
        staff.AvailableIntervals.Any(w => w.Start <= start && end <= w.End);

    private static bool OutsideOpeningHours(SlotPerformerInput staff, DateTimeOffset start, DateTimeOffset end) =>
        staff.AvailableIntervals.Any(w => w.Start <= start && end <= w.End && w.OutsideOpeningHours);
}
