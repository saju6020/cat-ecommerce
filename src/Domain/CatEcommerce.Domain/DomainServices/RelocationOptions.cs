using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BRD-001 section 8.15 (US-BRD-15 AC2) -- the units a stay could move to when its own unit is not free for an extension: active, of the SAME type (the stay was priced on that type's card), with
/// room for the cats, and free of every allocation and maintenance block for the remaining range. Deterministic order: block, sort order, code. Pure.
/// </summary>
public static class RelocationOptions
{
    public static IReadOnlyList<AvailabilityUnit> Find(BoardingInputSet inputs, BoardingUnitType type, int catCount, OccupancyRange remaining, Guid excludeUnitId) =>
        inputs.Units
            .Where(u => u.IsActive && u.Type == type && u.UnitId != excludeUnitId && u.CapacityCats >= catCount)
            .Where(u => !inputs.BusyUnits.TryGetValue(u.UnitId, out var busy) || !busy.Any(range => range.Overlaps(remaining)))
            .OrderBy(u => u.Block, StringComparer.Ordinal).ThenBy(u => u.SortOrder).ThenBy(u => u.Code, StringComparer.Ordinal)
            .ToList();
}
