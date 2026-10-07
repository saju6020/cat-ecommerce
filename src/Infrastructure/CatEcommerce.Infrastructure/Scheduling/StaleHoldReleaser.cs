using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Scheduling;

/// <summary>HO-APT-001 §8.6/§8.7 (ADR-070). <c>AsNoTracking</c> reads only -- the caller loads each
/// returned id through <c>IAggregateRootRepository&lt;AppointmentAggregate&gt;</c> and calls
/// <c>ExpireHold</c>/<c>ReleaseHold</c> itself. Each expiry now commits in its own
/// <c>SaveChangesAsync</c>, via <c>SlotClaim.ExpireOverlappingStaleHoldsAsync</c> -- not "every
/// write stays inside one <c>SaveChangesAsync</c>" as this comment originally said; see ADR-070's
/// 2026-09-28 amendment.</summary>
public sealed class StaleHoldReleaser(CatEcommerceDbContext db) : IStaleHoldReleaser
{
    /// <inheritdoc />
    public bool HasPendingChanges() => db.ChangeTracker.HasChanges();


    public async Task<IReadOnlyList<Guid>> FindStaleOverlappingHoldIdsAsync(
        DateTimeOffset start, DateTimeOffset end, Guid? performerId, Guid? roomId, Guid catId,
        DateTimeOffset now, Guid? excludeAppointmentId, CancellationToken ct = default)
    {
        var query = db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Held && a.HoldExpiresOn <= now)
            .Where(a => a.StartsOn != null && a.StartsOn < end && a.EndsOn > start)
            .Where(a => (performerId != null && a.PerformerId == performerId) ||
                        (roomId != null && a.RoomId == roomId) ||
                        a.CatId == catId);

        if (excludeAppointmentId is { } excludeId)
            query = query.Where(a => a.Id != excludeId);

        return await query.OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(ct);
    }

    public async Task<bool> TryCommitExpiryAsync(AggregateRoot stale, Func<Task> commit)
    {
        try
        {
            await commit();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed this hold first. Drop our now-stale tracked copy (and its
            // owned history) so nothing later in this request tries to save it again.
            db.Entry(stale).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<Guid?> FindActiveHoldIdForCustomerAsync(Guid customerId, Guid excludeAppointmentId, CancellationToken ct = default) =>
        await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => a.CustomerId == customerId && a.Status == AppointmentStatus.Held && a.Id != excludeAppointmentId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
}
