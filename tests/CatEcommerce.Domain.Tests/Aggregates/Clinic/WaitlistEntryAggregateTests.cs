using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Clinic;

/// <summary>HO-APT-001 §8.11 -- the waitlist entry's own state machine.</summary>
public sealed class WaitlistEntryAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static WaitlistEntryAggregate WaitingEntry()
    {
        var entry = new WaitlistEntryAggregate(Guid.NewGuid());
        entry.Join(new WaitlistJoinDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(Now.Date), null, Now, TestUserContext.Create(), Guid.NewGuid()));
        return entry;
    }

    [Fact]
    public void Join_SetsWaitingStatus()
    {
        var entry = WaitingEntry();

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        Assert.Equal(Now, entry.CreatedOn);
    }

    [Fact]
    public void Offer_FromWaiting_TransitionsToOffered()
    {
        var entry = WaitingEntry();
        var appointmentId = Guid.NewGuid();

        entry.Offer(new WaitlistOfferDto(appointmentId, Now.AddMinutes(30), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(WaitlistStatus.Offered, entry.Status);
        Assert.Equal(appointmentId, entry.OfferedAppointmentId);
    }

    [Fact]
    public void Claim_FromOffered_TransitionsToClaimed()
    {
        var entry = WaitingEntry();
        entry.Offer(new WaitlistOfferDto(Guid.NewGuid(), Now.AddMinutes(30), TestUserContext.Create(), Guid.NewGuid()));

        entry.Claim(new WaitlistClaimDto(TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(WaitlistStatus.Claimed, entry.Status);
    }

    [Fact]
    public void Claim_WhenNotOffered_IsANoOp()
    {
        var entry = WaitingEntry();

        entry.Claim(new WaitlistClaimDto(TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
    }

    /// <summary>US-CLN-09 AC2 -- an unclaimed offer lapses; Lapsed is terminal (OQ-8).</summary>
    [Fact]
    public void Lapse_FromOffered_TransitionsToLapsed_AndClearsOffer()
    {
        var entry = WaitingEntry();
        entry.Offer(new WaitlistOfferDto(Guid.NewGuid(), Now.AddMinutes(30), TestUserContext.Create(), Guid.NewGuid()));

        entry.Lapse(new WaitlistLapseDto(TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(WaitlistStatus.Lapsed, entry.Status);
        Assert.Null(entry.OfferedAppointmentId);
        Assert.Null(entry.OfferExpiresOn);
    }

    [Fact]
    public void Withdraw_FromWaiting_TransitionsToWithdrawn()
    {
        var entry = WaitingEntry();

        entry.Withdraw(new WaitlistWithdrawDto(TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(WaitlistStatus.Withdrawn, entry.Status);
    }

    [Fact]
    public void Close_FromWaiting_TransitionsToClosed()
    {
        var entry = WaitingEntry();

        entry.Close(new WaitlistCloseDto(TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(WaitlistStatus.Closed, entry.Status);
    }

    [Fact]
    public void ReassignOwner_ChangesCustomerId()
    {
        var entry = WaitingEntry();
        var newCustomerId = Guid.NewGuid();

        entry.ReassignOwner(newCustomerId);

        Assert.Equal(newCustomerId, entry.CustomerId);
    }
}
