using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

public enum AvailabilityMode
{
    /// <summary>A customer on the web: horizon, check-in cut-off, and isolation units never offered (ADR-227).</summary>
    Online,

    /// <summary>Reception: no horizon, may offer isolation units to a stay that needs isolation.</summary>
    Staff,
}

public sealed record AvailabilityUnit(Guid UnitId, string Code, BoardingUnitType Type, int CapacityCats, string? Block, int SortOrder, bool IsActive);

/// <summary>Everything the finder needs, pre-loaded (HO-BRD-001 section 8.8). <see cref="BusyUnits"/> maps a unit to its active allocations and live maintenance
/// blocks as instant ranges, already excluding the stay being changed.</summary>
public sealed record AvailabilityRequest(
    DateOnly ArrivalOn, DateOnly DepartureOn, int CatCount, DateTimeOffset Now, string TimeZoneId,
    TimeRange CheckInWindow, TimeRange CheckOutWindow, int TurnaroundHours,
    int MaxStayNights, int BookingHorizonDays, int AlternativeSearchDays,
    IReadOnlyList<AvailabilityUnit> Units,
    IReadOnlyDictionary<Guid, IReadOnlyList<OccupancyRange>> BusyUnits,
    bool RequiresIsolation, AvailabilityMode Mode,
    IReadOnlyDictionary<int, RateCardSnapshot> RateCards, PricingSnapshot Pricing);

/// <summary>One candidate unit and whether it is free for the requested window (staff mode shows per-unit freedom).</summary>
public sealed record UnitFreedom(Guid UnitId, string Code, BoardingUnitType Type, int CapacityCats, string? Block, bool IsFree);

/// <summary><see cref="AllocatedUnitId"/> is the unit the allocator would take -- deterministic, so a reserve takes the unit that was priced.</summary>
public sealed record UnitTypeAvailability(
    BoardingUnitType UnitType, int CapacityCats, int FreeUnitCount, Guid? AllocatedUnitId, BoardingQuote Quote);

public sealed record AlternativeDates(DateOnly ArrivalOn, DateOnly DepartureOn, BoardingUnitType CheapestType, decimal Total);

public sealed record AvailabilityResult(
    StayDateViolation? Violation, OccupancyRange Window, int Nights,
    IReadOnlyList<UnitTypeAvailability> UnitTypes, IReadOnlyList<UnitFreedom> Units,
    IReadOnlyList<AlternativeDates> Alternatives, bool WaitlistAvailable);

/// <summary>
/// HO-BRD-001 section 8.8 (US-BRD-02, ADR-220, renumbered ADR-353) -- availability is COMPUTED, never stored, and never in SQL. Pure.
/// <list type="number">
/// <item>The window is <see cref="BoardingOccupancyWindow"/>; a unit is free when no busy range overlaps <c>[From, To)</c> (US-BRD-02 AC2).</item>
/// <item>Candidates are active units with capacity for the cats.</item>
/// <item>Isolation units are offered only when the stay requires isolation -- and then ONLY isolation units; online mode never offers them.</item>
/// <item>Per type: the free count and the first free unit by block, sort order, code -- deterministic.</item>
/// <item>A type with no rate card in force is omitted (not bookable).</item>
/// <item>Online: the date rules of <see cref="StayDateRules"/> apply (horizon, today's check-in cut-off).</item>
/// <item>Nothing free: up to three nearest alternative ranges of the same length within the search radius, and the waitlist is offered (US-BRD-02 AC3).</item>
/// </list>
/// </summary>
public static class BoardingAvailabilityFinder
{
    public const int MaxAlternatives = 3;

    public static AvailabilityResult Find(AvailabilityRequest request)
    {
        var violation = StayDateRules.Validate(
            request.ArrivalOn, request.DepartureOn, request.Now, request.TimeZoneId, request.CheckInWindow,
            request.MaxStayNights, request.BookingHorizonDays, isOnline: request.Mode == AvailabilityMode.Online);

        var nights = Math.Max(0, StayDateRules.Nights(request.ArrivalOn, request.DepartureOn));
        if (violation is not null)
            return new AvailabilityResult(violation, default, nights, [], [], [], false);

        var window = WindowOf(request, request.ArrivalOn, request.DepartureOn);
        var candidates = Candidates(request);
        var units = candidates
            .Select(u => new UnitFreedom(u.UnitId, u.Code, u.Type, u.CapacityCats, u.Block, IsFree(request, u.UnitId, window)))
            .ToList();

        var types = TypesFor(request, candidates, window, nights);
        var nothingFree = types.All(t => t.FreeUnitCount == 0);

        var alternatives = nothingFree ? FindAlternatives(request, candidates, nights) : [];
        return new AvailabilityResult(null, window, nights, types, units, alternatives, WaitlistAvailable: nothingFree);
    }

    /// <summary>Candidate units for the request: active, big enough, and of the right kind of unit (rule 3).</summary>
    public static IReadOnlyList<AvailabilityUnit> Candidates(AvailabilityRequest request)
    {
        var offerIsolation = request.RequiresIsolation && request.Mode == AvailabilityMode.Staff;

        return request.Units
            .Where(u => u.IsActive && u.CapacityCats >= request.CatCount)
            .Where(u => offerIsolation ? u.Type == BoardingUnitType.Isolation : u.Type != BoardingUnitType.Isolation)
            .OrderBy(u => u.Block, StringComparer.Ordinal).ThenBy(u => u.SortOrder).ThenBy(u => u.Code, StringComparer.Ordinal)
            .ToList();
    }

    public static bool IsFree(AvailabilityRequest request, Guid unitId, OccupancyRange window) =>
        !request.BusyUnits.TryGetValue(unitId, out var busy) || !busy.Any(range => range.Overlaps(window));

    public static OccupancyRange WindowOf(AvailabilityRequest request, DateOnly arrivalOn, DateOnly departureOn) =>
        BoardingOccupancyWindow.Compute(arrivalOn, departureOn, request.TimeZoneId, request.CheckInWindow, request.CheckOutWindow, request.TurnaroundHours);

    private static List<UnitTypeAvailability> TypesFor(AvailabilityRequest request, IReadOnlyList<AvailabilityUnit> candidates, OccupancyRange window, int nights) =>
        candidates
            .GroupBy(u => u.Type.Value)
            .OrderBy(g => g.Key)
            .Where(g => request.RateCards.ContainsKey(g.Key))
            .Select(g =>
            {
                var free = g.Where(u => IsFree(request, u.UnitId, window)).ToList();
                var card = request.RateCards[g.Key];
                var quote = BoardingQuoteCalculator.Quote(nights, request.CatCount, card, request.Pricing);
                return new UnitTypeAvailability(g.First().Type, g.Max(u => u.CapacityCats), free.Count, free.FirstOrDefault()?.UnitId, quote);
            })
            .ToList();

    private static List<AlternativeDates> FindAlternatives(AvailabilityRequest request, IReadOnlyList<AvailabilityUnit> candidates, int nights)
    {
        var found = new List<AlternativeDates>();

        for (var shift = 1; shift <= request.AlternativeSearchDays && found.Count < MaxAlternatives; shift++)
        {
            foreach (var days in new[] { -shift, shift })
            {
                if (found.Count >= MaxAlternatives) break;

                var arrival = request.ArrivalOn.AddDays(days);
                var departure = request.DepartureOn.AddDays(days);
                if (StayDateRules.Validate(arrival, departure, request.Now, request.TimeZoneId, request.CheckInWindow,
                        request.MaxStayNights, request.BookingHorizonDays, isOnline: request.Mode == AvailabilityMode.Online) is not null)
                    continue;

                var window = WindowOf(request, arrival, departure);
                var types = TypesFor(request, candidates, window, nights).Where(t => t.FreeUnitCount > 0).ToList();
                if (types.Count == 0) continue;

                var cheapest = types.OrderBy(t => t.Quote.Total).ThenBy(t => t.UnitType.Value).First();
                found.Add(new AlternativeDates(arrival, departure, cheapest.UnitType, cheapest.Quote.Total));
            }
        }

        return found;
    }
}
