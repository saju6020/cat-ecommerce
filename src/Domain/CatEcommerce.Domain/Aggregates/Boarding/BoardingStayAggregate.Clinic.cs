using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Aggregates.Boarding;

// HO-BRD-001 Phase G (US-BRD-14, US-SRG-09): referral to the clinic, the vet spending limit and approvals, and surgery on a boarded cat.
public sealed partial class BoardingStayAggregate
{
    private readonly List<VetReferral> _referrals = [];
    private readonly List<VetChargeCommitment> _commitments = [];
    private readonly List<VetChargeApproval> _approvals = [];
    private readonly List<StaySurgery> _surgeries = [];

    public IReadOnlyList<VetReferral> Referrals => _referrals;
    public IReadOnlyList<VetChargeCommitment> Commitments => _commitments;
    public IReadOnlyList<VetChargeApproval> Approvals => _approvals;
    public IReadOnlyList<StaySurgery> Surgeries => _surgeries;

    /// <summary>
    /// A boarded cat is sent to the clinic. Only a checked-in stay can refer; the cat must be on it and not already at the clinic. Raises
    /// <see cref="BoardedCatReferredToClinicEvent"/> -- HO-EMR-001 subscribes and opens the visit (section 12.8 E1).
    /// </summary>
    public VetReferral ReferCat(ReferDto dto)
    {
        if (Status != BoardingStayStatus.CheckedIn)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferralStayNotInHouse, "A cat can be referred to the clinic only while it is boarding.", nameof(Status));

        var cat = FindCat(dto.CatId);
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > IncidentRules.MaxReferralReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReferral, $"A reason is required (up to {IncidentRules.MaxReferralReasonLength} characters).", nameof(dto.Reason));
        if (_referrals.Any(r => r.CatId == cat.CatId && r.IsOpen))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReferral, $"{cat.CatName} is already at the clinic.", nameof(dto.CatId));
        if (_referrals.Count >= StayRules.MaxReferrals)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReferral, "This stay has reached the limit of referrals.", nameof(dto.CatId));

        var referral = new VetReferral(Guid.NewGuid(), cat.CatId, dto.Reason.Trim(), dto.Urgency, dto.UserContext.UserId, dto.Now);
        _referrals.Add(referral);

        AddDomainEvent(new BoardedCatReferredToClinicEvent
        {
            ReferralId = referral.Id, StayId = Id, CatId = cat.CatId, CustomerId = CustomerId, BranchId = BranchId, Urgency = dto.Urgency,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
        return referral;
    }

    /// <summary>The cat is back from the clinic: it is no longer away from its unit. Idempotent.</summary>
    public void ReturnCatFromClinic(Guid referralId, DateTimeOffset now, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        var referral = _referrals.FirstOrDefault(r => r.Id == referralId)
            ?? throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReferral, "That referral is not on this stay.", nameof(referralId));
        if (!referral.IsOpen) return;

        referral.Return(now);
        AddDomainEvent(new CatReturnedFromClinicEvent { ReferralId = referral.Id, StayId = Id, CatId = referral.CatId, AwayFrom = referral.ReferredOn, AwayTo = now, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>HO-EMR-001 opened the visit of a referral. Idempotent; an unknown referral is a no-op (the event may belong to another stay).</summary>
    public bool LinkReferralVisit(Guid referralId, Guid visitId)
    {
        var referral = _referrals.FirstOrDefault(r => r.Id == referralId);
        if (referral is null || referral.VisitId is not null) return false;

        referral.LinkVisit(visitId);
        UpdateVersion();
        return true;
    }

    /// <summary>The cat's vet spending headroom now (section 8.14, AC2): <c>limit = (authorised ? limit : 0) + approved increases</c>, <c>headroom = limit - committed</c>.</summary>
    public Models.VetSpendingHeadroomResult GetHeadroom(Guid catId) =>
        VetSpendingHeadroom.Compute(FindCat(catId), _approvals, _commitments, CurrencyCode);

    /// <summary>A clinic charge on a boarded cat counts against its limit. Idempotent by charge id.</summary>
    public bool RecordVetCommitment(RecordVetCommitmentDto dto)
    {
        if (_commitments.Any(c => c.ChargeId == dto.ChargeId)) return false;

        FindCat(dto.CatId);
        if (dto.Amount < 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, "A charge cannot be negative.", nameof(dto.Amount));
        if (_commitments.Count >= StayRules.MaxVetCommitments)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, "This stay has reached the limit of recorded clinic charges.", nameof(dto.ChargeId));

        _commitments.Add(new VetChargeCommitment(Guid.NewGuid(), dto.ChargeId, dto.CatId, dto.Amount, dto.Now));
        UpdateVersion();
        return true;
    }

    /// <summary>A charge above the limit needs the owner's approval; the treatment is held until the decision (HO-EMR-001, section 12.8 E4).</summary>
    public VetChargeApproval RequestVetApproval(RequestVetApprovalDto dto)
    {
        if (Status != BoardingStayStatus.CheckedIn)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferralStayNotInHouse, "An approval can be requested only while the cat is boarding.", nameof(Status));

        var cat = FindCat(dto.CatId);
        if (dto.Amount <= 0m || dto.Amount > BillingRules.MaxAmount || decimal.Round(dto.Amount, 2) != dto.Amount)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, "The amount must be above zero, with at most two decimals.", nameof(dto.Amount));
        if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().Length > IncidentRules.MaxVetApprovalDescriptionLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, $"A description is required (up to {IncidentRules.MaxVetApprovalDescriptionLength} characters).", nameof(dto.Description));
        if (dto.ReferralId is { } referralId && _referrals.All(r => r.Id != referralId))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, "That referral is not on this stay.", nameof(dto.ReferralId));
        if (_approvals.Count >= StayRules.MaxApprovals)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, "This stay has reached the limit of approval requests.", nameof(dto.CatId));

        var approval = new VetChargeApproval(Guid.NewGuid(), cat.CatId, dto.ReferralId, dto.Amount, dto.Description.Trim(), dto.UserContext.UserId, dto.Now);
        _approvals.Add(approval);

        AddDomainEvent(new VetChargeApprovalRequestedEvent { ApprovalId = approval.Id, StayId = Id, CatId = cat.CatId, Amount = approval.Amount, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
        return approval;
    }

    /// <summary>The owner answers on the portal, or staff record a phone answer (a note is then required, and it is attributed). A decision is final.</summary>
    public VetChargeApproval DecideVetApproval(DecideVetApprovalDto dto)
    {
        var approval = _approvals.FirstOrDefault(a => a.Id == dto.ApprovalId)
            ?? throw new DomainRuleViolationException(BusinessViolationCodes.VetApprovalNotFound, "Approval request not found.", nameof(dto.ApprovalId));
        if (approval.Status != VetApprovalStatus.Pending)
            throw new DomainRuleViolationException(BusinessViolationCodes.VetApprovalAlreadyDecided, "This request has already been answered.", nameof(dto.ApprovalId));
        if (dto.Via == AcknowledgementChannel.Staff && string.IsNullOrWhiteSpace(dto.Note))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, "A note is required when recording an answer given by phone.", nameof(dto.Note));
        if (dto.Note is { Length: > IncidentRules.MaxApprovalNoteLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidVetApproval, "The note is too long.", nameof(dto.Note));

        approval.Decide(dto.Approved, dto.UserContext.UserId, dto.Via, dto.Note, dto.Now);

        AddDomainEvent(new VetChargeApprovalDecidedEvent
        {
            ApprovalId = approval.Id, StayId = Id, CatId = approval.CatId, Approved = dto.Approved, Amount = approval.Amount, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
        return approval;
    }

    // ── surgery on a boarded cat (US-SRG-09) ─────────────────────────────────────────────────────────────────────────

    /// <summary>HO-SRG-001 scheduled a surgery for a cat in this stay. A surgery never releases the unit. Idempotent by surgery id (a reschedule updates the start).</summary>
    public bool RecordSurgery(Guid surgeryId, Guid catId, DateTimeOffset startsOn, Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        if (Status != BoardingStayStatus.CheckedIn) return false;

        var existing = _surgeries.FirstOrDefault(s => s.Id == surgeryId);
        if (existing is not null)
        {
            if (existing.StartsOn == startsOn) return false;

            existing.Reschedule(startsOn);
            UpdateVersion();
            return true;
        }

        FindCat(catId);
        _surgeries.Add(new StaySurgery(surgeryId, catId, startsOn));
        AddDomainEvent(new StaySurgeryRecordedEvent { StayId = Id, SurgeryId = surgeryId, CatId = catId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
        return true;
    }

    /// <summary>The surgery ended: the cat is back. Idempotent; an unknown surgery is a no-op.</summary>
    public bool DischargeSurgery(Guid surgeryId, DateTimeOffset on, Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        var surgery = _surgeries.FirstOrDefault(s => s.Id == surgeryId);
        if (surgery is null || surgery.DischargedOn is not null) return false;

        surgery.Discharge(on);
        AddDomainEvent(new StaySurgeryDischargedEvent { StayId = Id, SurgeryId = surgeryId, CatId = surgery.CatId, AwayFrom = surgery.StartsOn, AwayTo = on, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
        return true;
    }

    /// <summary>
    /// Whether a cat is away from its unit at <paramref name="at"/>: at the clinic (an open referral) or in theatre (a surgery that has started and not been discharged). A cat that is away
    /// cannot be checked out (<c>CatNotPresent</c>), and the occupancy shows "Away" -- the unit is NOT released.
    /// </summary>
    public bool IsAway(Guid catId, DateTimeOffset at) => AwayReason(catId, at) is not null;

    public string? AwayReason(Guid catId, DateTimeOffset at) =>
        StayAwayRule.Reason(catId, at, _referrals.Select(r => (r.CatId, r.IsOpen)), _surgeries.Select(s => (s.CatId, s.StartsOn, s.DischargedOn)));
}
