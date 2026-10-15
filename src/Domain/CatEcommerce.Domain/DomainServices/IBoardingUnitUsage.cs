namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BRD-001 section 11.4 (F32): what the stays say about a unit, as a read-only port, so the unit and maintenance handlers (which sit beside the
/// stay aggregate in the Boarding context) never load a stay to answer it. Active allocations are those that hold the unit in the exclusion
/// constraint (<see cref="StayTransitionRules.HoldsOccupancy"/>).
/// </summary>
public interface IBoardingUnitUsage
{
    /// <summary>True when an active allocation of the unit ends after <paramref name="now"/>.</summary>
    Task<bool> HasFutureAllocationsAsync(Guid unitId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>The arrival dates (branch-local, from the stay) of every stay with an active allocation overlapping <paramref name="range"/>.</summary>
    Task<IReadOnlyList<DateOnly>> GetOverlappingStayArrivalsAsync(Guid unitId, OccupancyRange range, CancellationToken ct = default);
}
