using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Scheduling;

/// <summary>HO-APT-001 §8.10/§11.6. EF cannot translate a per-row <see cref="TimeZoneInfo"/>
/// conversion, so each method loads a generously UTC-buffered candidate set, then filters to the
/// exact local date/time client-side using <see cref="BranchCalendar.LocalDateOf"/> -- correct, at
/// the cost of a slightly wider initial query.</summary>
public sealed class FlaggableAppointmentFinder(CatEcommerceDbContext db) : IFlaggableAppointmentFinder
{
    public async Task<IReadOnlyList<Guid>> FindByPerformerInRangeAsync(Guid performerId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (rangeStart, rangeEnd) = BufferedUtcRange(from, to);

        var candidates = await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.PerformerId == performerId && a.Status == AppointmentStatus.Booked)
            .Where(a => a.StartsOn != null && a.StartsOn >= rangeStart && a.StartsOn < rangeEnd)
            .Select(a => new { a.Id, a.BranchId, StartsOn = a.StartsOn!.Value })
            .ToListAsync(ct);

        if (candidates.Count == 0) return [];

        var branchZones = await LoadBranchZonesAsync(candidates.Select(c => c.BranchId).Distinct(), ct);

        return candidates
            .Where(c => IsWithinLocalDateRange(c.StartsOn, branchZones.GetValueOrDefault(c.BranchId, "UTC"), from, to))
            .Select(c => c.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> FindByBranchInRangeAsync(Guid branchId, DateOnly from, DateOnly to, TimeOnly? partialFrom, TimeOnly? partialTo, CancellationToken ct = default)
    {
        var (rangeStart, rangeEnd) = BufferedUtcRange(from, to);

        var candidateRows = await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.BranchId == branchId && a.Status == AppointmentStatus.Booked)
            .Where(a => a.StartsOn != null && a.StartsOn >= rangeStart && a.StartsOn < rangeEnd)
            .Select(a => new { a.Id, StartsOn = a.StartsOn!.Value })
            .ToListAsync(ct);

        if (candidateRows.Count == 0) return [];

        var branch = await db.Set<BranchAggregate>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, ct);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return candidateRows
            .Where(c =>
            {
                var local = TimeZoneInfo.ConvertTime(c.StartsOn, timeZone);
                var localDate = DateOnly.FromDateTime(local.DateTime);
                if (localDate < from || localDate > to) return false;
                if (partialFrom is null || partialTo is null) return true;

                var localTime = TimeOnly.FromDateTime(local.DateTime);
                return localTime >= partialFrom.Value && localTime < partialTo.Value;
            })
            .Select(c => c.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> FindFutureByCatAsync(Guid catId, DateTimeOffset now, CancellationToken ct = default) =>
        await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.CatId == catId && a.Status == AppointmentStatus.Booked && a.StartsOn != null && a.StartsOn > now)
            .Select(a => a.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindFlaggedBySourceAsync(Guid sourceId, CancellationToken ct = default) =>
        await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.NeedsRescheduling && a.RescheduleFlagSourceId == sourceId)
            .Select(a => a.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.CustomerId == customerId)
            .Select(a => a.Id)
            .ToListAsync(ct);

    private static (DateTimeOffset Start, DateTimeOffset End) BufferedUtcRange(DateOnly from, DateOnly to) =>
        (new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
         new DateTimeOffset(to.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    private static bool IsWithinLocalDateRange(DateTimeOffset instant, string timeZoneId, DateOnly from, DateOnly to)
    {
        var localDate = BranchCalendar.LocalDateOf(instant, timeZoneId);
        return localDate >= from && localDate <= to;
    }

    private async Task<Dictionary<Guid, string>> LoadBranchZonesAsync(IEnumerable<Guid> branchIds, CancellationToken ct)
    {
        var ids = branchIds.ToList();
        return await db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.TimeZoneId, ct);
    }
}
