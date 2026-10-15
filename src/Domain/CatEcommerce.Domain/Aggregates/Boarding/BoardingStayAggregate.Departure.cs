using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Aggregates.Boarding;

// HO-BRD-001 Phase G (US-BRD-15, US-BRD-16, US-BRD-17): extension, check-out (early or on time) and the stay's charge bookkeeping.
public sealed partial class BoardingStayAggregate
{
    /// <summary>The revision of the stay's charges last pushed to Billing (0 = none yet). Part of the charge source key, so a re-derived charge has a fresh key.</summary>
    public int ChargeRevision { get; private set; }

    /// <summary>The credit owed for invoiced charges that no longer apply after an early check-out (section 8.15). Staff issue the credit note in Billing; Boarding only records and shows it.</summary>
    public decimal CreditDue { get; private set; }

    /// <summary>
    /// US-BRD-15 AC1. A confirmed or checked-in stay is extended to a later departure on its CURRENT allocation: the allocation and the cats' occupancy ranges grow, the stay is re-quoted with
    /// its own rate-card snapshot, and the gate is re-evaluated as of the new departure. The handler has already checked the unit is free for the extra range; the exclusion constraint is the
    /// control. The stay's charges are re-derived by the handler (<see cref="ApplyChargePlan"/>).
    /// </summary>
    public void Extend(ExtendDto dto)
    {
        if (Status != BoardingStayStatus.Confirmed && Status != BoardingStayStatus.CheckedIn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "extended"), nameof(Status));

        if (dto.NewDepartureOn <= DepartureOn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayDates, "The new departure must be after the current departure.", "NewDepartureOn");

        var nights = StayDateRules.Nights(ArrivalOn, dto.NewDepartureOn);
        if (nights > dto.MaxStayNights)
            throw new DomainRuleViolationException(BusinessViolationCodes.StayTooLong, $"A stay can be at most {dto.MaxStayNights} nights.", "NewDepartureOn");

        var current = CurrentAllocation
            ?? throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, "This stay holds no unit.", nameof(Status));

        ApplyGates(dto.Gates, dto.Today);
        var blocked = _cats.Where(c => c.GateStatus == VaccinationGateStatus.Blocked).Select(c => c.CatName).ToList();
        if (blocked.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.VaccinationGateBlocked,
                $"{string.Join(", ", blocked)} {(blocked.Count == 1 ? "has" : "have")} a core vaccination that is due before the new departure.", "CatIds");

        DepartureOn = dto.NewDepartureOn;
        OccupiedTo = dto.Window.To;
        current.SetRange(current.OccupiedFrom, dto.Window.To);
        ApplyQuote(dto.Quote);
        SyncOccupancyFlags();

        AddDomainEvent(new BoardingStayExtendedEvent { StayId = Id, NewDepartureOn = DepartureOn, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>
    /// US-BRD-16 / US-BRD-17. CheckedIn -> CheckedOut against a checklist per cat -- weight, every belonging ticked (returned, or a note why not) and the owner's signature -- and only when no cat is
    /// away (<c>CatNotPresent</c>). An incomplete checklist names each missing item per cat and leaves the stay checked in. Before the booked departure it is an EARLY check-out: the stay is
    /// re-quoted for the nights charged (the basis is recalculated), the departure becomes today and the allocation is truncated so the unit frees at the check-out instant plus the turnaround.
    /// </summary>
    public void CheckOut(CheckOutDto dto)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.CheckOut);

        var away = _cats.Where(c => IsAway(c.CatId, dto.Now)).Select(c => c.CatName).ToList();
        if (away.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.CatNotPresent, $"{string.Join(", ", away)} {(away.Count == 1 ? "is" : "are")} not in the unit at the moment. Check out once back.", "CatIds");

        var problems = new List<string>();
        foreach (var cat in _cats)
        {
            var input = dto.Cats.FirstOrDefault(c => c.CatId == cat.CatId);
            var missing = input is null ? ["everything"] : MissingCheckOutItems(cat, input);
            if (missing.Count > 0) problems.Add($"{cat.CatName}: {string.Join(", ", missing)}");
        }

        if (problems.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.CheckOutChecklistIncomplete, "The check-out checklist is incomplete. Missing - " + string.Join("; ", problems) + ".", "Cats");

        foreach (var cat in _cats)
            cat.RecordCheckOut(dto.Cats.First(c => c.CatId == cat.CatId), dto.Now);

        var early = dto.EarlyQuote is not null && dto.ChargedDepartureOn < DepartureOn;
        var late = dto.EarlyQuote is not null && dto.ChargedDepartureOn > DepartureOn;
        var lateUnit = late ? CurrentAllocation?.UnitId : null;
        var lateFrom = OccupiedTo;
        if (dto.EarlyQuote is not null)
        {
            DepartureOn = dto.ChargedDepartureOn;
            OccupiedTo = dto.TruncatedWindow.To;
            var current = CurrentAllocation;
            current?.SetRange(current.OccupiedFrom, dto.TruncatedWindow.To);
            ApplyQuote(dto.EarlyQuote!);
        }

        CheckedOutOn = dto.Now;
        Transition(StayTransitionRules.CheckOut, dto.Now, dto.UserContext.UserId);

        AddDomainEvent(new BoardingStayCheckedOutEvent { StayId = Id, BranchId = BranchId, CustomerId = CustomerId, Early = early,
            Late = late, OverstayUnitId = lateUnit, OverstayFrom = late ? lateFrom : null, OverstayTo = late ? dto.Now : null, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        foreach (var cat in _cats)
            AddDomainEvent(new StayCatCheckedOutEvent { StayId = Id, CatId = cat.CatId, WeightKg = cat.CheckOutWeightKg!.Value, RecordedOn = dto.Today, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });

        UpdateVersion();
    }

    /// <summary>Records the revision of the charges pushed to Billing and the credit owed for invoiced charges that no longer apply. Idempotent for an unchanged plan.</summary>
    public void ApplyChargePlan(int revision, decimal creditDue, Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        if (revision == ChargeRevision && creditDue == CreditDue) return;

        var revisionChanged = revision != ChargeRevision;
        ChargeRevision = revision;
        CreditDue = creditDue;

        if (revisionChanged)
            AddDomainEvent(new StayChargesChangedEvent { StayId = Id, Revision = revision, CorrelationId = correlationId, UserContext = userContext });

        UpdateVersion();
    }

    private void ApplyQuote(BoardingQuote quote)
    {
        RateBasis = quote.Basis;
        _rateUnits.Clear();
        _rateUnits.AddRange(quote.Units);
        QuoteTotal = quote.Total;
        DiscountTotal = quote.Discount;
    }

    private static List<string> MissingCheckOutItems(Entities.StayCat cat, CheckOutCatDto input)
    {
        var missing = new List<string>();
        if (input.WeightKg is null || input.WeightKg < MinWeightKg || input.WeightKg > MaxWeightKg) missing.Add("weight");

        var unaccounted = cat.Belongings.Count(b => input.Belongings.All(r => r.BelongingId != b.Id));
        if (unaccounted > 0) missing.Add($"belongings ({unaccounted} not accounted for)");

        var unexplained = input.Belongings.Count(r => !r.Returned && (string.IsNullOrWhiteSpace(r.Note) || r.Note.Trim().Length > CareProfileRules.MaxBelongingDescriptionLength));
        if (unexplained > 0) missing.Add("a note for each belonging not returned");

        if (input.Signature is null || input.Signature.Validate() is not null) missing.Add("owner signature");
        return missing;
    }
}
