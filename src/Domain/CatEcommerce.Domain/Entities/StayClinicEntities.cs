using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-BRD-001 section 8.14 (ADR-229, renumbered ADR-362) -- a boarded cat sent to the clinic. Owned by the stay (at most twenty). <see cref="VisitId"/> is stored when HO-EMR-001
/// opens the visit (<c>ReferralVisitOpenedEvent</c>); <see cref="ReturnedOn"/> ends the time the cat is away from its unit.
/// </summary>
public sealed class VetReferral
{
    public Guid Id { get; private set; }
    public Guid CatId { get; private set; }
    public string Reason { get; private set; } = default!;
    public TriagePriority Urgency { get; private set; } = default!;
    public Guid ReferredBy { get; private set; }
    public DateTimeOffset ReferredOn { get; private set; }
    public Guid? VisitId { get; private set; }
    public DateTimeOffset? ReturnedOn { get; private set; }

    private VetReferral() { }

    public VetReferral(Guid id, Guid catId, string reason, TriagePriority urgency, Guid referredBy, DateTimeOffset referredOn)
    {
        Id = id;
        CatId = catId;
        Reason = reason;
        Urgency = urgency;
        ReferredBy = referredBy;
        ReferredOn = referredOn;
    }

    public bool IsOpen => ReturnedOn is null;

    internal void LinkVisit(Guid visitId) => VisitId ??= visitId;

    internal void Return(DateTimeOffset on) => ReturnedOn ??= on;
}

/// <summary>A clinic charge counted against a cat's vet spending limit; one per Billing charge id (idempotent). Owned by the stay (at most one hundred).</summary>
public sealed class VetChargeCommitment
{
    public Guid Id { get; private set; }
    public Guid ChargeId { get; private set; }
    public Guid CatId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }

    private VetChargeCommitment() { }

    public VetChargeCommitment(Guid id, Guid chargeId, Guid catId, decimal amount, DateTimeOffset recordedOn)
    {
        Id = id;
        ChargeId = chargeId;
        CatId = catId;
        Amount = amount;
        RecordedOn = recordedOn;
    }
}

/// <summary>An owner's decision on a vet charge above the cat's spending limit. Owned by the stay (at most twenty). A decision is final (<c>VetApprovalAlreadyDecided</c>).</summary>
public sealed class VetChargeApproval
{
    public Guid Id { get; private set; }
    public Guid CatId { get; private set; }
    public Guid? ReferralId { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = default!;
    public VetApprovalStatus Status { get; private set; } = default!;
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public DateTimeOffset? DecidedOn { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public AcknowledgementChannel? DecidedVia { get; private set; }
    public string? DecisionNote { get; private set; }

    private VetChargeApproval() { }

    public VetChargeApproval(Guid id, Guid catId, Guid? referralId, decimal amount, string description, Guid requestedBy, DateTimeOffset requestedOn)
    {
        Id = id;
        CatId = catId;
        ReferralId = referralId;
        Amount = amount;
        Description = description;
        Status = VetApprovalStatus.Pending;
        RequestedBy = requestedBy;
        RequestedOn = requestedOn;
    }

    internal void Decide(bool approved, Guid by, AcknowledgementChannel via, string? note, DateTimeOffset on)
    {
        Status = approved ? VetApprovalStatus.Approved : VetApprovalStatus.Declined;
        DecidedBy = by;
        DecidedVia = via;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedOn = on;
    }
}

/// <summary>
/// HO-BRD-001 section 8.21 (US-SRG-09) -- a surgery on a boarded cat, recorded when HO-SRG-001 schedules it. The cat stays on its unit's books (a surgery never releases the unit) but is
/// <b>away</b> from <see cref="StartsOn"/> until <see cref="DischargedOn"/>. Owned by the stay.
/// </summary>
public sealed class StaySurgery
{
    public Guid Id { get; private set; }
    public Guid CatId { get; private set; }
    public DateTimeOffset StartsOn { get; private set; }
    public DateTimeOffset? DischargedOn { get; private set; }

    private StaySurgery() { }

    public StaySurgery(Guid surgeryId, Guid catId, DateTimeOffset startsOn)
    {
        Id = surgeryId;
        CatId = catId;
        StartsOn = startsOn;
    }

    internal void Reschedule(DateTimeOffset startsOn) => StartsOn = startsOn;

    internal void Discharge(DateTimeOffset on) => DischargedOn ??= on;
}
