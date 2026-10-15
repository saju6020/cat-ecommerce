using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.18 (US-BRD-02 AC3, ADR-234/235, renumbered ADR-367/368) -- a customer asks to be told when a unit frees for a range. <c>Waiting</c> -> <c>Notified</c>;
/// <c>Waiting</c>/<c>Notified</c> -> <c>Withdrawn</c>; <c>Waiting</c> -> <c>Closed</c> once the arrival date has passed. There is NO HOLD: the first to book wins. One live entry per
/// customer and range is enforced by the filtered unique index <c>ix_boarding_waitlist_active</c>.
/// </summary>
public sealed class BoardingWaitlistEntryAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly ArrivalOn { get; private set; }
    public DateOnly DepartureOn { get; private set; }
    public int CatCount { get; private set; }
    public BoardingUnitType? UnitType { get; private set; }
    public BoardingWaitlistStatus Status { get; private set; } = default!;
    public DateTimeOffset JoinedOn { get; private set; }
    public DateTimeOffset? NotifiedOn { get; private set; }
    public DateTimeOffset? EndedOn { get; private set; }

    private BoardingWaitlistEntryAggregate() { }

    public BoardingWaitlistEntryAggregate(Guid id) : base(id) { }

    public bool IsLive => Status == BoardingWaitlistStatus.Waiting || Status == BoardingWaitlistStatus.Notified;

    public void Join(Guid branchId, Guid customerId, DateOnly arrivalOn, DateOnly departureOn, int catCount, BoardingUnitType? unitType, DateOnly today, DateTimeOffset now, UserContext userContext)
    {
        if (departureOn <= arrivalOn || arrivalOn < today)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayDates, "Give a future arrival and a later departure.", nameof(arrivalOn));
        if (catCount < 1 || catCount > StayRules.MaxCatsPerStay)
            throw new DomainRuleViolationException(BusinessViolationCodes.CatNotBoardable, $"A booking has between 1 and {StayRules.MaxCatsPerStay} cats.", nameof(catCount));

        BranchId = branchId;
        CustomerId = customerId;
        ArrivalOn = arrivalOn;
        DepartureOn = departureOn;
        CatCount = catCount;
        UnitType = unitType;
        Status = BoardingWaitlistStatus.Waiting;
        JoinedOn = now;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    /// <summary>A free unit has appeared for the range: the owner is told once. Anything but a waiting entry is a no-op.</summary>
    public bool MarkNotified(DateTimeOffset now)
    {
        if (Status != BoardingWaitlistStatus.Waiting) return false;

        Status = BoardingWaitlistStatus.Notified;
        NotifiedOn = now;
        UpdateVersion();
        return true;
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (!IsLive) return;

        Status = BoardingWaitlistStatus.Withdrawn;
        EndedOn = now;
        UpdateVersion();
    }

    /// <summary>The arrival date passed while the entry was still waiting.</summary>
    public bool CloseIfStale(DateOnly today, DateTimeOffset now)
    {
        if (Status != BoardingWaitlistStatus.Waiting || ArrivalOn >= today) return false;

        Status = BoardingWaitlistStatus.Closed;
        EndedOn = now;
        UpdateVersion();
        return true;
    }

    /// <summary>HO-ACC-001 ADR-039: a merged duplicate's entries follow the survivor. Idempotent.</summary>
    public void ReassignOwner(Guid newCustomerId)
    {
        if (CustomerId == newCustomerId) return;

        CustomerId = newCustomerId;
        UpdateVersion();
    }
}
