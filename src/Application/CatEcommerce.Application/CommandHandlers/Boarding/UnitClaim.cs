using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>
/// HO-BRD-001 section 11.4 (not a service layer -- a static helper over <see cref="IBoardingInputs"/>): the ONE sequence every handler that TAKES A UNIT follows.
/// 1. load the inputs for the branch and range; 2. validate with <see cref="BoardingAvailabilityFinder"/> in the handler's mode -- the type's deterministic unit
/// is free of allocations AND maintenance, of an allowed type, with capacity for the cats; 3. check each cat is in no other live stay overlapping the range
/// (<c>CatAlreadyBoarding</c>). The caller then mutates and saves ONCE; a lost race is the exclusion constraint's <c>23P01</c>, answered <c>409</c> by the
/// constraint filter. The pre-check gives the clean answer; the constraint is the control (ADR-219, renumbered ADR-352).
/// </summary>
public static class UnitClaim
{
    public sealed record Request(
        Guid BranchId, DateOnly ArrivalOn, DateOnly DepartureOn, BoardingUnitType UnitType, IReadOnlyCollection<Guid> CatIds,
        bool RequiresIsolation, AvailabilityMode Mode, Guid? ExcludeStayId, DateTimeOffset Now, Guid? TargetUnitId = null);

    public sealed record Outcome(
        string? Code, string? Message, string? Property, BoardingInputSet? Inputs, AllocatedUnit? Unit, UnitTypeAvailability? Type, OccupancyRange Window)
    {
        public bool IsSuccess => Code is null;

        public static Outcome Fail(string code, string message, string property, BoardingInputSet? inputs = null) =>
            new(code, message, property, inputs, null, null, default);
    }

    public static async Task<Outcome> ValidateAsync(IBoardingInputs boardingInputs, Request request, CancellationToken ct = default)
    {
        var inputs = await boardingInputs.LoadAsync(request.BranchId, request.ArrivalOn, request.DepartureOn, request.ExcludeStayId, request.Now, ct);
        if (inputs is null) return Outcome.Fail(BusinessViolationCodes.BranchNotFound, "Branch not found.", "BranchId");

        var s = inputs.Settings;
        var result = BoardingAvailabilityFinder.Find(new AvailabilityRequest(
            request.ArrivalOn, request.DepartureOn, request.CatIds.Count, request.Now, s.TimeZoneId, s.CheckInWindow, s.CheckOutWindow, s.TurnaroundHours,
            s.MaxStayNights, s.BookingHorizonDays, s.AlternativeSearchDays, inputs.Units, inputs.BusyUnits, request.RequiresIsolation, request.Mode,
            inputs.RateCards, s.Pricing));

        if (result.Violation is not null)
            return Outcome.Fail(result.Violation.Code, result.Violation.Message, result.Violation.Property, inputs);

        if (request.RequiresIsolation && request.UnitType != BoardingUnitType.Isolation)
            return Outcome.Fail(BusinessViolationCodes.IsolationUnitRequired, BoardingMessages.IsolationUnitRequired, "UnitType", inputs);

        var ofType = inputs.Units.Where(u => u.IsActive && u.Type == request.UnitType).ToList();
        if (ofType.Count == 0)
            return Outcome.Fail(BusinessViolationCodes.NoUnitAvailable, $"There is no {request.UnitType.DisplayName} unit at this branch.", "UnitType", inputs);

        if (!inputs.RateCards.ContainsKey(request.UnitType.Value))
            return Outcome.Fail(BusinessViolationCodes.UnitTypeNotPriced, $"{request.UnitType.DisplayName} units are not priced yet.", "UnitType", inputs);

        var type = result.UnitTypes.FirstOrDefault(t => t.UnitType == request.UnitType);
        if (type is null)
        {
            var capacity = ofType.Max(u => u.CapacityCats);
            return Outcome.Fail(BusinessViolationCodes.UnitCapacityExceeded,
                BoardingMessages.CapacityExceeded(request.UnitType.DisplayName, capacity, request.CatIds.Count), "CatIds", inputs);
        }

        if (type.FreeUnitCount == 0 || type.AllocatedUnitId is not { } unitId)
            return Outcome.Fail(BusinessViolationCodes.UnitNoLongerAvailable, BoardingMessages.UnitNoLongerAvailable, "UnitId", inputs);

        var chosenId = unitId;
        if (request.TargetUnitId is { } targetId)
        {
            var target = result.Units.FirstOrDefault(u => u.UnitId == targetId);
            if (target is null || !target.IsFree || target.Type != request.UnitType)
                return Outcome.Fail(BusinessViolationCodes.UnitNoLongerAvailable, BoardingMessages.UnitNoLongerAvailable, "UnitId", inputs);

            chosenId = targetId;
        }

        var overlaps = await boardingInputs.FindCatOverlapsAsync(request.CatIds, result.Window, request.ExcludeStayId, ct);
        if (overlaps.Count > 0)
        {
            var dates = string.Join(", ", overlaps.Select(o => $"{o.ArrivalOn:yyyy-MM-dd} to {o.DepartureOn:yyyy-MM-dd}").Distinct());
            return Outcome.Fail(BusinessViolationCodes.CatAlreadyBoarding, $"{BoardingMessages.CatAlreadyBoarding} ({dates})", "CatIds", inputs);
        }

        var unit = inputs.Units.First(u => u.UnitId == chosenId);
        return new Outcome(null, null, null, inputs, new AllocatedUnit(unit.UnitId, unit.Type, unit.CapacityCats), type, result.Window);
    }
}
