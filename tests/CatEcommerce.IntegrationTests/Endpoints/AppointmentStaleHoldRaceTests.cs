using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// Backlog row #31 (docs/backlog/APT.md, HO-APT-001): stale-hold expiry holds row locks until
/// commit, so (a) the stale holds must be found and locked in a fixed order (ORDER BY id) and (b) a
/// request that loses the race for a hold's row -- another request settled it first, so the xmin
/// check fails -- must carry on, not surface the generic "changed by someone else" 409.
/// Driven straight against the real database through separate DI scopes (separate DbContexts), the
/// way two concurrent requests would see it.
/// </summary>
public sealed class AppointmentStaleHoldRaceTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly UserContext SystemContext = new(Guid.NewGuid(), Guid.NewGuid());

    private static ServiceSnapshot Snapshot() => new("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT"));

    private static async Task SaveHoldAsync(
        IServiceProvider services, Guid id, Guid roomId, Guid performerId, Guid catId, DateTimeOffset startsOn, DateTimeOffset holdExpiresOn)
    {
        var repository = services.GetRequiredService<IAggregateRootRepository<AppointmentAggregate>>();
        var hold = new AppointmentAggregate(id);
        hold.PlaceHold(new SlotHoldDto(
            Guid.NewGuid(), Guid.NewGuid(), catId, Guid.NewGuid(), Snapshot(), performerId, roomId,
            startsOn, startsOn.AddMinutes(30), BookingChannel.Online, null, holdExpiresOn, SystemContext, Guid.NewGuid()));
        await repository.SaveAsync(hold, EventPublishOption.InMemoryPublishOnly);
    }

    /// <summary>An id whose first byte is <paramref name="rank"/> and the rest random: Postgres orders
    /// uuids bytewise, so the rank decides the ORDER BY id position.</summary>
    private static Guid RankedId(byte rank)
    {
        var raw = Guid.NewGuid().ToString("N").Substring(2);
        return Guid.Parse($"{rank:x2}{raw}");
    }

    [Fact]
    public async Task FindStaleOverlappingHoldIds_AreReturnedInIdOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var roomId = Guid.NewGuid();
        var firstStart = now.AddDays(60);
        var low = RankedId(0x01);
        var middle = RankedId(0x02);
        var high = RankedId(0x03);

        await using (var seed = Factory.Services.CreateAsyncScope())
        {
            // Inserted high -> middle -> low, so heap (insertion) order is the reverse of id order.
            await SaveHoldAsync(seed.ServiceProvider, high, roomId, Guid.NewGuid(), Guid.NewGuid(), firstStart, now.AddMinutes(-30));
            await SaveHoldAsync(seed.ServiceProvider, middle, roomId, Guid.NewGuid(), Guid.NewGuid(), firstStart.AddHours(1), now.AddMinutes(-30));
            await SaveHoldAsync(seed.ServiceProvider, low, roomId, Guid.NewGuid(), Guid.NewGuid(), firstStart.AddHours(2), now.AddMinutes(-30));
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var releaser = scope.ServiceProvider.GetRequiredService<IStaleHoldReleaser>();
        var ids = await releaser.FindStaleOverlappingHoldIdsAsync(
            firstStart, firstStart.AddHours(5), performerId: null, roomId, Guid.NewGuid(), now, excludeAppointmentId: null);

        Assert.Equal([low, middle, high], ids);
    }

    [Fact]
    public async Task ExpireOverlappingStaleHolds_WhenAnotherRequestSettlesTheHoldFirst_CarriesOn()
    {
        var now = DateTimeOffset.UtcNow;
        var holdId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var performerId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var startsOn = now.AddDays(61);

        await using (var seed = Factory.Services.CreateAsyncScope())
            await SaveHoldAsync(seed.ServiceProvider, holdId, roomId, performerId, catId, startsOn, now.AddMinutes(-30));

        // Request 1 (its own DbContext) has already loaded the stale hold ...
        await using var firstScope = Factory.Services.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IAggregateRootRepository<AppointmentAggregate>>();
        var firstCopy = await firstRepository.GetByIdAsync(holdId);
        Assert.NotNull(firstCopy);

        // ... when request 2 expires and commits it.
        await using (var secondScope = Factory.Services.CreateAsyncScope())
        {
            var secondRepository = secondScope.ServiceProvider.GetRequiredService<IAggregateRootRepository<AppointmentAggregate>>();
            var secondCopy = await secondRepository.GetByIdAsync(holdId);
            secondCopy!.ExpireHold(now);
            await secondRepository.UpdateAsync(secondCopy, EventPublishOption.InMemoryPublishOnly);
        }

        // Request 1 now reaches its own expiry of the same hold: its xmin check fails. It must carry
        // on rather than throw DbUpdateConcurrencyException (the generic 409).
        var releaser = new FixedIdsReleaser(firstScope.ServiceProvider.GetRequiredService<IStaleHoldReleaser>(), [holdId]);
        var slot = new Slot(startsOn, startsOn.AddMinutes(30), performerId, "Vet", roomId, "Room", false);

        await SlotClaim.ExpireOverlappingStaleHoldsAsync(releaser, firstRepository, slot, catId, now, excludeAppointmentId: null);

        var db = firstScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        Assert.Equal(EntityState.Detached, db.Entry(firstCopy!).State);

        await using var verify = Factory.Services.CreateAsyncScope();
        var settled = await verify.ServiceProvider.GetRequiredService<IAggregateRootRepository<AppointmentAggregate>>().GetByIdAsync(holdId);
        Assert.Equal(AppointmentStatus.HoldExpired, settled!.Status);
        Assert.Single(settled.StatusHistory, h => h.To == AppointmentStatus.HoldExpired);
    }

    /// <summary>Reports a fixed set of stale ids (the finder having run before the other request's
    /// commit) but commits through the real releaser.</summary>
    private sealed class FixedIdsReleaser(IStaleHoldReleaser inner, IReadOnlyList<Guid> ids) : IStaleHoldReleaser
    {
        public Task<IReadOnlyList<Guid>> FindStaleOverlappingHoldIdsAsync(
            DateTimeOffset start, DateTimeOffset end, Guid? performerId, Guid? roomId, Guid catId,
            DateTimeOffset now, Guid? excludeAppointmentId, CancellationToken ct = default) => Task.FromResult(ids);

        public Task<Guid?> FindActiveHoldIdForCustomerAsync(Guid customerId, Guid excludeAppointmentId, CancellationToken ct = default) =>
            inner.FindActiveHoldIdForCustomerAsync(customerId, excludeAppointmentId, ct);

        public bool HasPendingChanges() => inner.HasPendingChanges();

        public Task<bool> TryCommitExpiryAsync(AggregateRoot stale, Func<Task> commit) => inner.TryCommitExpiryAsync(stale, commit);
    }
}
