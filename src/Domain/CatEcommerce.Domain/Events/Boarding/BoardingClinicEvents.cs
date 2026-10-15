using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Boarding;

// HO-BRD-001 Phase G (section 11.5). Ids, instants, dates, enums, amounts and booleans only (U-X1).

/// <summary>HO-EMR-001 contract (section 12.8 E1): a boarded cat was referred to the clinic. EMR opens the visit and answers with <see cref="ReferralVisitOpenedEvent"/>.</summary>
public sealed class BoardedCatReferredToClinicEvent : Event
{
    public required Guid ReferralId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required TriagePriority Urgency { get; init; }
}

/// <summary>RAISED BY HO-EMR-001 (contract, section 12.8 E1) once it has opened the visit of a referral; <c>LinkReferralVisitHandler</c> stores the visit id on the referral.</summary>
public sealed class ReferralVisitOpenedEvent : Event
{
    public required Guid ReferralId { get; init; }
    public required Guid VisitId { get; init; }
}

/// <summary>RAISED BY HO-EMR-001 / HO-BIL-001 / HO-SRG-001 (contract, section 12.8): a clinic charge was recorded for a boarded cat; it counts against the cat's vet spending limit.</summary>
public sealed class BoardingVetChargeRecordedEvent : Event
{
    public required Guid ChargeId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>HO-EMR-001 contract (section 12.8 E4): the owner (or staff by phone) decided a charge above the limit. EMR holds the treatment until this event.</summary>
public sealed class VetChargeApprovalDecidedEvent : Event
{
    public required Guid ApprovalId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required bool Approved { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class VetChargeApprovalRequestedEvent : Event
{
    public required Guid ApprovalId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>The cat is back from the clinic. Care tasks that fell due while it was away are skipped with a reason (US-SRG-09 rule), not marked missed.</summary>
public sealed class CatReturnedFromClinicEvent : Event
{
    public required Guid ReferralId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required DateTimeOffset AwayFrom { get; init; }
    public required DateTimeOffset AwayTo { get; init; }
}

/// <summary>A surgery on a boarded cat ended. Care tasks due while the cat was in theatre are skipped; the unit was never released.</summary>
public sealed class StaySurgeryDischargedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required DateTimeOffset AwayFrom { get; init; }
    public required DateTimeOffset AwayTo { get; init; }
}

public sealed class BoardingStayExtendedEvent : Event
{
    public required Guid StayId { get; init; }
    public required DateOnly NewDepartureOn { get; init; }
}

/// <summary>A stay's charge lines were re-derived (extension, early check-out). HO-BIL-001 may use it to refresh a draft invoice.</summary>
public sealed class StayChargesChangedEvent : Event
{
    public required Guid StayId { get; init; }
    public required int Revision { get; init; }
}

public sealed class BoardingStayCheckedOutEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required bool Early { get; init; }

    /// <summary>True for a check-out after the booked departure. Then <see cref="OverstayUnitId"/> is the unit and [<see cref="OverstayFrom"/>, <see cref="OverstayTo"/>) the time the cat stayed past the booking:
    /// a booking of that unit overlapping it is flagged for staff (the allocation is never extended).</summary>
    public bool Late { get; init; }
    public Guid? OverstayUnitId { get; init; }
    public DateTimeOffset? OverstayFrom { get; init; }
    public DateTimeOffset? OverstayTo { get; init; }
}

/// <summary>HO-BIL-001 contract (section 12.8 P2, "Daycare: DaycareVisit source on check-out"): a daycare visit ended; its charge was pushed to Billing in the same save.</summary>
public sealed class DaycareVisitCheckedOutEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class StaySurgeryRecordedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
}
