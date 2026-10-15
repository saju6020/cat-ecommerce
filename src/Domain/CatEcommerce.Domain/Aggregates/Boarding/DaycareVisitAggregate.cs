using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.18 (FR-BRD-22, US-BRD-21, ADR-234/235) -- one cat's day at the clinic. It NEVER allocates a unit: capacity is <c>boarding.daycareDailyCapacity</c> visits per branch per day,
/// counted by the handler and raced by the unique slot row <c>(branch_id, local_date, slot_number)</c>. The price is snapshotted from the rate card in force when the visit is booked. Charged through
/// Billing as source <c>DaycareVisit</c> when the cat is checked out.
/// </summary>
public sealed class DaycareVisitAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CatId { get; private set; }
    public string CatName { get; private set; } = default!;
    public DateOnly LocalDate { get; private set; }
    public int SlotNumber { get; private set; }
    public DaycareBasis Basis { get; private set; } = default!;
    public int? ExpectedHours { get; private set; }
    public DaycareVisitStatus Status { get; private set; } = default!;
    public decimal HourlyRate { get; private set; }
    public decimal DayRate { get; private set; }
    public Guid TaxClassId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public DateTimeOffset BookedOn { get; private set; }
    public DateTimeOffset? CheckedInOn { get; private set; }
    public DateTimeOffset? CheckedOutOn { get; private set; }
    public decimal? ChargeAmount { get; private set; }

    private DaycareVisitAggregate() { }

    public DaycareVisitAggregate(Guid id) : base(id) { }

    public void Book(
        Guid branchId, Guid customerId, Guid catId, string catName, DateOnly localDate, int slotNumber, DaycareBasis basis, int? expectedHours, DaycareRateCardAggregate card,
        DateOnly today, DateTimeOffset now, UserContext userContext)
    {
        if (localDate < today)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayDates, "Daycare cannot be booked for a past day.", nameof(localDate));
        if (basis == DaycareBasis.Hourly && (expectedHours is null or < 1 or > 12))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayDates, "Give the expected hours (1 to 12).", nameof(expectedHours));

        BranchId = branchId;
        CustomerId = customerId;
        CatId = catId;
        CatName = catName;
        LocalDate = localDate;
        SlotNumber = slotNumber;
        Basis = basis;
        ExpectedHours = basis == DaycareBasis.Hourly ? expectedHours : null;
        Status = DaycareVisitStatus.Booked;
        HourlyRate = card.HourlyRate;
        DayRate = card.DayRate;
        TaxClassId = card.TaxClassId;
        CurrencyCode = card.CurrencyCode;
        BookedOn = now;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    /// <summary>On the day of the visit only.</summary>
    public void CheckIn(DateOnly today, DateTimeOffset now)
    {
        if (Status != DaycareVisitStatus.Booked)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "checked in"), nameof(Status));
        if (today != LocalDate)
            throw new DomainRuleViolationException(BusinessViolationCodes.CheckInNotOnArrivalDate, "A daycare visit can be checked in only on its day.", nameof(LocalDate));

        Status = DaycareVisitStatus.CheckedIn;
        CheckedInOn = now;
        UpdateVersion();
    }

    public void CheckOut(DateTimeOffset now, UserContext userContext, Guid correlationId)
    {
        if (Status != DaycareVisitStatus.CheckedIn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "checked out"), nameof(Status));

        Status = DaycareVisitStatus.CheckedOut;
        CheckedOutOn = now;
        ChargeAmount = DaycarePricing.Charge(Basis, HourlyRate, DayRate, now - CheckedInOn!.Value);

        AddDomainEvent(new DaycareVisitCheckedOutEvent
        {
            VisitId = Id, BranchId = BranchId, CustomerId = CustomerId, Amount = ChargeAmount.Value, CorrelationId = correlationId, UserContext = userContext
        });
        UpdateVersion();
    }

    /// <summary>Cancelling frees the slot (the slot index excludes cancelled visits).</summary>
    public void Cancel()
    {
        if (Status != DaycareVisitStatus.Booked)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "cancelled"), nameof(Status));

        Status = DaycareVisitStatus.Cancelled;
        UpdateVersion();
    }

    /// <summary>Only once the day has passed without a check-in.</summary>
    public void RecordNoShow(DateOnly today)
    {
        if (Status != DaycareVisitStatus.Booked)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "marked as a no-show"), nameof(Status));
        if (today <= LocalDate)
            throw new DomainRuleViolationException(BusinessViolationCodes.NoShowTooEarly, "It is too early to record a no-show for this visit.", nameof(LocalDate));

        Status = DaycareVisitStatus.NoShow;
        UpdateVersion();
    }

    /// <summary>HO-ACC-001 ADR-039: a merged duplicate's visits follow the survivor. Idempotent.</summary>
    public void ReassignOwner(Guid newCustomerId)
    {
        if (CustomerId == newCustomerId) return;

        CustomerId = newCustomerId;
        UpdateVersion();
    }
}
