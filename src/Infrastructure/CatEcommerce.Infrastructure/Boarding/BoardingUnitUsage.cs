using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-BRD-001 section 11.4 (F32): what the stays say about a unit -- read over <c>boarding.stay_unit_allocation</c>, untracked and projected. An allocation
/// counts only while it is active (reserved, confirmed, checked in or checked out); an enquiry, a cancelled stay and a no-show hold nothing.
/// </summary>
public sealed class BoardingUnitUsage(CatEcommerceDbContext db) : IBoardingUnitUsage
{
    public Task<bool> HasFutureAllocationsAsync(Guid unitId, DateTimeOffset now, CancellationToken ct = default) =>
        db.BoardingStays.AsNoTracking()
            .SelectMany(s => s.Allocations)
            .AnyAsync(a => a.UnitId == unitId && a.IsActive && a.OccupiedTo > now, ct);

    public async Task<IReadOnlyList<DateOnly>> GetOverlappingStayArrivalsAsync(Guid unitId, OccupancyRange range, CancellationToken ct = default)
    {
        var arrivals = await db.BoardingStays.AsNoTracking()
            .Where(s => s.Allocations.Any(a => a.UnitId == unitId && a.IsActive && a.OccupiedFrom < range.To && range.From < a.OccupiedTo))
            .Select(s => s.ArrivalOn)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

        return arrivals;
    }
}
