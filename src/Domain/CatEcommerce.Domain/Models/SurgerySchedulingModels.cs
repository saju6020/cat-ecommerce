using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

// HO-SRG-001 §8.5 -- inputs and results of SurgerySlotFinder and the ISurgerySchedulingInputs port.

/// <summary>What a busy interval is, so a clash message can name it ("Nurse Akter is assisting another surgery 10:00-11:00").</summary>
public enum SurgeryBusyKind
{
    /// <summary>The staff member is the performer of an ordinary appointment.</summary>
    AsPerformer,

    /// <summary>The staff member is the surgeon of another surgery.</summary>
    AsSurgeon,

    /// <summary>The staff member is the assistant of another surgery.</summary>
    AsAssistant,

    /// <summary>A theatre or cat interval (no role).</summary>
    Booking,
}

public sealed record SurgeryBusyInterval(DateTimeOffset Start, DateTimeOffset End, SurgeryBusyKind Kind);

public sealed record SurgeryProcedureInfo(int DurationMinutes, IReadOnlyList<string> QualifiedRoleIds);

public sealed record SurgerySlotRequest(
    SurgeryProcedureInfo Procedure,
    string TimeZoneId,
    SlotPerformerInput? Surgeon,
    SlotPerformerInput? Assistant,
    SlotRoomInput? Theatre,
    IReadOnlyList<SurgeryBusyInterval> SurgeonBusy,
    IReadOnlyList<SurgeryBusyInterval> AssistantBusy,
    IReadOnlyList<SurgeryBusyInterval> TheatreBusy,
    IReadOnlyList<SurgeryBusyInterval> CatBusy,
    int GridMinutes,
    DateTimeOffset EarliestStart);

public sealed record SurgerySlot(DateTimeOffset StartsOn, DateTimeOffset EndsOn, bool OutsideOpeningHours);

/// <summary>One reason a start is refused. <paramref name="Property"/> is the request field it concerns (<c>theatreId</c>, <c>surgeonId</c>, <c>assistantId</c>, <c>catId</c>, <c>startsOn</c>).</summary>
public sealed record SurgeryClash(string Code, string Property, string Message);

public sealed record SurgeryInputRequest(
    Guid BranchId, Guid ServiceId, DateOnly LocalDate, Guid? SurgeonId, Guid? AssistantId, Guid? TheatreId, Guid? CatId, Guid? ExcludeAppointmentId);

public sealed record SurgeryInputSet(
    string TimeZoneId,
    SchedulingServiceInfo? Service,
    SlotPerformerInput? Surgeon,
    SlotPerformerInput? Assistant,
    SlotRoomInput? Theatre,
    IReadOnlyList<SurgeryBusyInterval> SurgeonBusy,
    IReadOnlyList<SurgeryBusyInterval> AssistantBusy,
    IReadOnlyList<SurgeryBusyInterval> TheatreBusy,
    IReadOnlyList<SurgeryBusyInterval> CatBusy);

/// <summary>HO-SRG-001 §8.11 (ADR-171) -- what <c>ISurgeryCampaignPricing</c> is asked: the standard price at now, and the campaign (if any) the caller wants applied.</summary>
public sealed record SurgeryPricingRequest(
    Guid BranchId, Guid ServiceId, DateOnly LocalDate, Money StandardPrice, Guid? CampaignId, string? CampaignEligibilityNote);

/// <summary>Either the snapshot to store on the surgery, or the reason a campaign does not apply (<c>CampaignNotApplicable</c>, with the reason in the message).</summary>
public sealed record SurgeryPricingResult(SurgeryPriceSnapshot? Snapshot, SurgeryClash? Refusal);
