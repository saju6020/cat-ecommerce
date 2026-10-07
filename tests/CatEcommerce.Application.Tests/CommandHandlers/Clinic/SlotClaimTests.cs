using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Clinic;

/// <summary>ADR-070's 2026-09-28 amendment (owner decision, ARCHITECTURE gate second pass) --
/// <c>SlotClaim.ExpireOverlappingStaleHoldsAsync</c> commits each stale hold's expiry in its own
/// save, so it enforces that no caller has pending tracked changes on entry.</summary>
public sealed class SlotClaimTests
{
    private static readonly Slot AnySlot = new(
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), Guid.NewGuid(), "Vet", Guid.NewGuid(), "Room", false);

    [Fact]
    public async Task ExpireOverlappingStaleHoldsAsync_WithPendingChanges_ThrowsInvalidOperationException()
    {
        var staleHoldReleaser = new FakeStaleHoldReleaser { PendingChanges = true };
        var appointmentRepository = new FakeAggregateRootRepository<AppointmentAggregate>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SlotClaim.ExpireOverlappingStaleHoldsAsync(
                staleHoldReleaser, appointmentRepository, AnySlot, Guid.NewGuid(), DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public async Task ExpireOverlappingStaleHoldsAsync_WithNoPendingChanges_Completes()
    {
        var staleHoldReleaser = new FakeStaleHoldReleaser { PendingChanges = false };
        var appointmentRepository = new FakeAggregateRootRepository<AppointmentAggregate>();

        await SlotClaim.ExpireOverlappingStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, AnySlot, Guid.NewGuid(), DateTimeOffset.UtcNow, null);

        Assert.Empty(appointmentRepository.Updated);
    }

    /// <summary>Backlog #31 -- losing the race for a stale hold's row to another request (the commit
    /// reports it was settled first) is not an error: the claim carries on with the next hold.</summary>
    [Fact]
    public async Task ExpireOverlappingStaleHoldsAsync_WhenTheCommitLosesTheRace_CarriesOnToTheNextHold()
    {
        var now = DateTimeOffset.UtcNow;
        var appointmentRepository = new FakeAggregateRootRepository<AppointmentAggregate>();
        var first = StaleHold(now);
        var second = StaleHold(now);
        appointmentRepository.Seed(first);
        appointmentRepository.Seed(second);
        var staleHoldReleaser = new FakeStaleHoldReleaser { StaleIds = [first.Id, second.Id], LoseTheRace = true };

        await SlotClaim.ExpireOverlappingStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, AnySlot, Guid.NewGuid(), now, null);

        Assert.Equal(AppointmentStatus.HoldExpired, first.Status);
        Assert.Equal(AppointmentStatus.HoldExpired, second.Status);
        Assert.Empty(appointmentRepository.Updated);
    }

    private static AppointmentAggregate StaleHold(DateTimeOffset now)
    {
        var hold = new AppointmentAggregate(Guid.NewGuid());
        hold.PlaceHold(new SlotHoldDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new ServiceSnapshot("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT")),
            Guid.NewGuid(), Guid.NewGuid(), now.AddDays(1), now.AddDays(1).AddMinutes(30), BookingChannel.Online, null,
            now.AddMinutes(-5), TestUserContext.Create(), Guid.NewGuid()));
        return hold;
    }
}
