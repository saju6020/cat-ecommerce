using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Aggregates.Boarding;

// HO-BRD-001 Phase E (US-BRD-08, US-BRD-18): check-in against a checklist, isolation, and relocation.
public sealed partial class BoardingStayAggregate
{
    public const decimal MinWeightKg = 0.10m;
    public const decimal MaxWeightKg = 30.00m;

    /// <summary>
    /// Confirmed -> CheckedIn, only on the arrival date (branch-local) or the day after (a late arrival), only when EVERY cat's checklist is complete -- identity,
    /// vaccination proof, weight, arrival health, belongings (listed, or "none") with photos when the branch requires them, and the owner's signature -- and the
    /// gate still passes (US-BRD-08 AC2). An incomplete checklist names each missing item per cat and leaves the stay Confirmed.
    /// </summary>
    public void CheckIn(CheckInDto dto)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.CheckIn);

        if (dto.Today < ArrivalOn || dto.Today > ArrivalOn.AddDays(1))
            throw new DomainRuleViolationException(BusinessViolationCodes.CheckInNotOnArrivalDate, "Check-in is only possible on the arrival date, or the day after for a late arrival.", "ArrivalOn");

        var problems = new List<string>();
        foreach (var cat in _cats)
        {
            var input = dto.Cats.FirstOrDefault(c => c.CatId == cat.CatId);
            var missing = input is null ? ["everything"] : MissingCheckInItems(input, dto.RequireBelongingsPhotos);
            if (missing.Count > 0) problems.Add($"{cat.CatName}: {string.Join(", ", missing)}");
        }

        if (problems.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.CheckInChecklistIncomplete, "The check-in checklist is incomplete. Missing - " + string.Join("; ", problems) + ".", "Cats");

        ApplyGates(dto.Gates, dto.Today);
        var blocked = _cats.Where(c => c.GateStatus == VaccinationGateStatus.Blocked).Select(c => c.CatName).ToList();
        if (blocked.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.VaccinationGateBlocked, $"{string.Join(", ", blocked)} has a core vaccination that is due before departure.", "CatIds");

        foreach (var cat in _cats)
            cat.RecordCheckIn(dto.Cats.First(c => c.CatId == cat.CatId), dto.Now);

        CheckedInOn = dto.Now;
        Transition(StayTransitionRules.CheckIn, dto.Now, dto.UserContext.UserId);

        AddDomainEvent(new BoardingStayCheckedInEvent { StayId = Id, BranchId = BranchId, CustomerId = CustomerId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        foreach (var cat in _cats)
            AddDomainEvent(new StayCatCheckedInEvent { StayId = Id, CatId = cat.CatId, WeightKg = cat.CheckInWeightKg!.Value, RecordedOn = dto.Today, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });

        UpdateVersion();
    }

    private static List<string> MissingCheckInItems(CheckInCatDto input, bool requirePhotos)
    {
        var missing = new List<string>();
        if (!input.IdentityConfirmed) missing.Add("identity");
        if (!input.VaccinationProofVerified) missing.Add("vaccination proof");
        if (input.WeightKg is null || input.WeightKg < MinWeightKg || input.WeightKg > MaxWeightKg) missing.Add("weight");
        if (string.IsNullOrWhiteSpace(input.ArrivalHealthObservation) || input.ArrivalHealthObservation.Trim().Length > 1000) missing.Add("arrival health");

        if (!input.NoBelongings && input.Belongings.Count == 0) missing.Add("belongings");
        else if (input.Belongings.Count > CareProfileRules.MaxBelongings || input.Belongings.Any(b => string.IsNullOrWhiteSpace(b.Description) || b.Description.Length > CareProfileRules.MaxBelongingDescriptionLength
                     || b.FileIds.Count > CareProfileRules.MaxBelongingPhotos)) missing.Add("belongings (invalid)");
        else if (requirePhotos && input.Belongings.Count > 0 && input.Belongings.All(b => b.FileIds.Count == 0)) missing.Add("belongings photos");

        if (input.Signature is null || input.Signature.Validate() is not null) missing.Add("owner signature");
        return missing;
    }

    /// <summary>
    /// US-BRD-18 / BR-9. Flags the cat. A stay with a suspected-infectious cat may sit only in an isolation unit, so when it is not in one the SAME mutation moves it
    /// (<see cref="FlagInfectiousDto.IsolationUnit"/>, chosen by the handler); with none free the flag is refused and nothing changes (the dashboard lists the cat).
    /// </summary>
    public void FlagSuspectedInfectious(FlagInfectiousDto dto)
    {
        if (Status != BoardingStayStatus.CheckedIn && Status != BoardingStayStatus.Confirmed && Status != BoardingStayStatus.Reserved)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "flagged as suspected infectious"), nameof(Status));

        if (string.IsNullOrWhiteSpace(dto.Note) || dto.Note.Trim().Length > StayRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCareProfile, "A note is required.", nameof(dto.Note));

        var cat = FindCat(dto.CatId);
        var current = CurrentAllocation;
        var inIsolation = current?.UnitType == BoardingUnitType.Isolation;

        if (!inIsolation)
        {
            if (dto.IsolationUnit is null)
                throw new DomainRuleViolationException(BusinessViolationCodes.NoIsolationUnitAvailable, "No isolation unit is free for this stay.", "TargetIsolationUnitId");

            MoveTo(dto.IsolationUnit, dto.Now, dto.Now, "Suspected infectious", dto.UserContext, requireIsolation: true);
            AddDomainEvent(new BoardingStayRelocatedEvent { StayId = Id, UnitId = dto.IsolationUnit.UnitId, FromInstant = dto.Now, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        }

        cat.FlagInfectious(dto.UserContext.UserId, dto.Now, dto.Note.Trim());
        RequiresIsolation = true;
        AddDomainEvent(new SuspectedInfectiousFlaggedEvent { StayId = Id, CatId = cat.CatId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Clearing the flag does not move the cat; it only ends the requirement once no cat of the stay is still flagged.</summary>
    public void ClearSuspectedInfectious(ClearInfectiousDto dto)
    {
        var cat = FindCat(dto.CatId);
        if (!cat.SuspectedInfectious) return;

        cat.ClearInfectious(string.IsNullOrWhiteSpace(dto.Note) ? "Cleared" : dto.Note.Trim());
        RequiresIsolation = _cats.Any(c => c.SuspectedInfectious);
        AddDomainEvent(new SuspectedInfectiousClearedEvent { StayId = Id, CatId = cat.CatId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>US-BRD-15 AC2: the stay moves to another unit from an instant, for a recorded reason; the quote does not change (same type price list).</summary>
    public void Relocate(RelocateDto dto)
    {
        if (Status != BoardingStayStatus.Confirmed && Status != BoardingStayStatus.CheckedIn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "relocated"), nameof(Status));

        MoveTo(dto.Unit, dto.FromInstant, dto.Now, dto.Reason, dto.UserContext, requireIsolation: RequiresIsolation);
        AddDomainEvent(new BoardingStayRelocatedEvent { StayId = Id, UnitId = dto.Unit.UnitId, FromInstant = dto.FromInstant, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private void MoveTo(AllocatedUnit target, DateTimeOffset fromInstant, DateTimeOffset now, string? reason, Platform.Infrastructure.Common.Security.UserContext by, bool requireIsolation)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleViolationException(BusinessViolationCodes.RelocationReasonRequired, "A reason is required to move a stay to another unit.", "Reason");

        if (requireIsolation && target.Type != BoardingUnitType.Isolation)
            throw new DomainRuleViolationException(BusinessViolationCodes.IsolationUnitRequired, BoardingMessages.IsolationUnitRequired, "TargetUnitId");

        if (target.CapacityCats < _cats.Count)
            throw new DomainRuleViolationException(BusinessViolationCodes.UnitCapacityExceeded, BoardingMessages.CapacityExceeded(target.Type.DisplayName, target.CapacityCats, _cats.Count), "TargetUnitId");

        var current = CurrentAllocation
            ?? throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, "This stay holds no unit.", nameof(Status));

        if (fromInstant < current.OccupiedFrom) fromInstant = current.OccupiedFrom;
        if (fromInstant >= current.OccupiedTo) fromInstant = current.OccupiedTo.AddTicks(-1);

        var originalEnd = current.OccupiedTo;
        if (fromInstant <= current.OccupiedFrom)
        {
            // Nothing of the current range has been used yet: replace it outright rather than leave an empty range behind.
            current.SetRange(current.OccupiedFrom, current.OccupiedFrom.AddTicks(1));
        }
        else
        {
            current.SetRange(current.OccupiedFrom, fromInstant);
        }

        _allocations.Add(new UnitAllocation(Guid.NewGuid(), target.UnitId, target.Type, fromInstant, originalEnd, StayTransitionRules.HoldsOccupancy(Status), reason.Trim(), by.UserId, now));
    }
}
