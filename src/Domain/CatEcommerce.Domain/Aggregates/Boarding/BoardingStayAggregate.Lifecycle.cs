using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Aggregates.Boarding;

// HO-BRD-001 Phase D (US-BRD-06, BR-12, BR-16, Appendix C): confirm, lapse, expire, cancel and no-show.
public sealed partial class BoardingStayAggregate
{
    /// <summary>
    /// Reserved -> Confirmed, only once Billing's ACCEPTED deposit covers the requirement (ADR-223, renumbered ADR-356). The gate is re-evaluated: a cat whose
    /// vaccination lapsed since the reservation refuses the confirmation. A payment awaiting a second approver is not accepted money (see
    /// <see cref="MarkDepositPendingApproval"/>).
    /// </summary>
    public void Confirm(ConfirmStayDto dto)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.Confirm);

        if (dto.AcceptedDeposit < DepositRequired)
            throw new DomainRuleViolationException(BusinessViolationCodes.DepositNotYetReceived,
                $"The deposit has not been received in full. Outstanding: {BoardingMoneyText(DepositRequired - dto.AcceptedDeposit)}.", "Deposit");

        ApplyGates(dto.Gates, dto.Today);
        var blocked = _cats.Where(c => c.GateStatus == VaccinationGateStatus.Blocked).Select(c => c.CatName).ToList();
        if (blocked.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.VaccinationGateBlocked,
                $"{string.Join(", ", blocked)} has a core vaccination due before departure.", "CatIds");

        if (DepositRequired > 0m) DepositState = DepositState.Received;
        DepositDueBy = null;
        ConfirmedOn = dto.Now;
        ConfirmedBy = dto.UserContext.UserId;
        Transition(StayTransitionRules.Confirm, dto.Now, dto.UserContext.UserId);

        AddDomainEvent(new BoardingStayConfirmedEvent { StayId = Id, BranchId = BranchId, CustomerId = CustomerId, ArrivalOn = ArrivalOn, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>While a payment awaits Billing's second approver the lapse is suspended -- money has been handed over (ADR-223). Idempotent.</summary>
    public void MarkDepositPendingApproval()
    {
        if (Status != BoardingStayStatus.Reserved || DepositState == DepositState.PendingApproval) return;
        if (DepositState != DepositState.Awaiting) return;

        DepositState = DepositState.PendingApproval;
        UpdateVersion();
    }

    /// <summary>Billing rejected the pending payment: the state returns to Awaiting with <see cref="DepositDueBy"/> unchanged, so it may lapse on the next pass.</summary>
    public void ClearDepositPendingApproval()
    {
        if (Status != BoardingStayStatus.Reserved || DepositState != DepositState.PendingApproval) return;

        DepositState = DepositState.Awaiting;
        UpdateVersion();
    }

    /// <summary>BR-16: an unconfirmed reservation does not hold a unit forever. Past <see cref="DepositDueBy"/> and not Received / PendingApproval it is Cancelled
    /// (<c>ReservationLapsed</c>); the unit is free at once because the allocations go inactive.</summary>
    public void Lapse(LapseStayDto dto)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.Lapse);

        if (DepositDueBy is not { } due || dto.Now < due || DepositState == DepositState.PendingApproval || DepositState == DepositState.Received)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReservationNotLapsable, "This reservation cannot lapse yet.", "DepositDueBy");

        CancelledOn = dto.Now;
        CancelledBy = dto.UserContext.UserId;
        CancellationReason = BoardingCancellationReason.ReservationLapsed;
        Transition(StayTransitionRules.Lapse, dto.Now, dto.UserContext.UserId);

        AddDomainEvent(new BoardingReservationLapsedEvent { StayId = Id, BranchId = BranchId, CustomerId = CustomerId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>An enquiry whose arrival date has passed is closed (Cancelled / EnquiryExpired); it never held a unit.</summary>
    public void ExpireEnquiry(LapseStayDto dto, DateOnly today)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.ExpireEnquiry);

        if (ArrivalOn >= today)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, "This enquiry has not passed its arrival date.", "ArrivalOn");

        CancelledOn = dto.Now;
        CancelledBy = dto.UserContext.UserId;
        CancellationReason = BoardingCancellationReason.EnquiryExpired;
        Transition(StayTransitionRules.ExpireEnquiry, dto.Now, dto.UserContext.UserId);
        UpdateVersion();
    }

    /// <summary>
    /// BR-12. A customer cancels their own; staff cancel with a note. The deposit is forfeited when the cancellation falls inside
    /// <c>boarding.cancellationWindowHours</c> of the arrival window; outside it, a deposit that was received is owed back (Billing refunds it).
    /// </summary>
    public void Cancel(CancelStayDto dto)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.Cancel);

        if (!dto.ByCustomer && string.IsNullOrWhiteSpace(dto.Note))
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired, "A reason is required to cancel a stay.", nameof(dto.Note));
        if (dto.Note is { Length: > StayRules.MaxCancellationNoteLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired, "The reason is too long.", nameof(dto.Note));

        var held = StayTransitionRules.HoldsOccupancy(Status);
        DepositForfeited = held && dto.Now > OccupiedFrom.AddHours(-dto.WindowHours);
        CancelledOn = dto.Now;
        CancelledBy = dto.UserContext.UserId;
        CancellationReason = dto.ByCustomer ? BoardingCancellationReason.ByCustomer : BoardingCancellationReason.ByStaff;
        CancellationNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        Transition(StayTransitionRules.Cancel, dto.Now, dto.UserContext.UserId);

        AddDomainEvent(new BoardingStayCancelledEvent
        {
            StayId = Id, BranchId = BranchId, CustomerId = CustomerId, DepositForfeited = DepositForfeited, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Confirmed -> NoShow, after the grace period past the start of the arrival window; the deposit is forfeited and the unit freed.</summary>
    public void RecordNoShow(NoShowDto dto)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.NoShow);

        if (dto.Now < OccupiedFrom.AddHours(dto.GraceHours))
            throw new DomainRuleViolationException(BusinessViolationCodes.NoShowTooEarly, "It is too early to record a no-show for this stay.", "ArrivalOn");

        DepositForfeited = true;
        NoShowRecordedOn = dto.Now;
        NoShowRecordedBy = dto.UserContext.UserId;
        Transition(StayTransitionRules.NoShow, dto.Now, dto.UserContext.UserId);

        AddDomainEvent(new BoardingStayNoShowRecordedEvent { StayId = Id, BranchId = BranchId, CustomerId = CustomerId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>
    /// A cat died or changed owner (HO-CAT-001 events): the stay is FLAGGED for staff -- it is never cancelled and nothing is sent (section 20.7). Idempotent; a flag already
    /// set for another reason is kept.
    /// </summary>
    public void FlagAttention(StayAttentionReason reason)
    {
        if (NeedsAttention) return;
        if (Status == BoardingStayStatus.Cancelled || Status == BoardingStayStatus.NoShow || Status == BoardingStayStatus.CheckedOut) return;

        NeedsAttention = true;
        AttentionReason = reason;
        UpdateVersion();
    }

    /// <summary>Staff have dealt with the flag. A note saying how is required and kept with who and when; the flag itself is cleared. Idempotent when nothing is flagged.</summary>
    public void ClearAttention(string note, DateTimeOffset now, Guid by)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > StayRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.OverrideReasonRequired, $"A note is required (at most {StayRules.MaxReasonLength} characters).", nameof(note));
        if (!NeedsAttention) return;

        NeedsAttention = false;
        AttentionReason = null;
        AttentionClearedOn = now;
        AttentionClearedBy = by;
        AttentionClearedNote = note.Trim();
        UpdateVersion();
    }

    /// <summary>HO-ACC-001 ADR-039: a merged duplicate's stays follow the survivor. Idempotent.</summary>
    public void ReassignOwner(Guid newCustomerId)
    {
        if (CustomerId == newCustomerId) return;

        CustomerId = newCustomerId;
        UpdateVersion();
    }

    private void ApplyGates(IReadOnlyDictionary<Guid, GateEvaluation> gates, DateOnly today)
    {
        foreach (var cat in _cats)
            if (gates.TryGetValue(cat.CatId, out var gate)) cat.ApplyGate(gate, today);
    }

    private string BoardingMoneyText(decimal amount) => $"{CurrencyCode} {amount:0.00}";
}
