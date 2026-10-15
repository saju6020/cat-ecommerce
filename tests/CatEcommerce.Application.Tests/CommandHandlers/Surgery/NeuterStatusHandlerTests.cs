using CatEcommerce.Application.DomainEventHandlers.Cat;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Surgery;

/// <summary>HO-SRG-001 §8.14 (US-SRG-06, ADR-166) -- U-E7: the Cat-context handler sets the neuter status from a completed sterilisation, idempotently, and never fails the request for a deceased cat.</summary>
public sealed class NeuterStatusHandlerTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private sealed class FixedCatClock(DateOnly today) : CatEcommerce.Domain.DomainServices.ICatRecordClock
    {
        public Task<DateOnly> LocalTodayAsync(CancellationToken ct) => Task.FromResult(today);
    }

    private sealed class FixedIp : CatEcommerce.Domain.DomainServices.IClientIpAccessor
    {
        public System.Net.IPAddress? GetClientIpAddress() => null;
    }

    private static CatAggregate Cat()
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(Guid.NewGuid(), "Mitu", "Cat", CatSex.Female, new DateOnly(2024, 1, 1), DateOfBirthPrecision.Exact, "DSH", "Tabby", Today, TestUserContext.Create(), Guid.NewGuid()));
        return cat;
    }

    private static (SetNeuterStatusOnSterilisationHandler Handler, FakeAggregateRootRepository<CatAggregate> Cats, FakeAggregateRootRepository<AuditEntryAggregate> Audit) Build(CatAggregate cat, DateOnly? clockToday = null)
    {
        var cats = new FakeAggregateRootRepository<CatAggregate>();
        var audit = new FakeAggregateRootRepository<AuditEntryAggregate>();
        cats.Seed(cat);
        return (new SetNeuterStatusOnSterilisationHandler(NullLogger<SetNeuterStatusOnSterilisationHandler>.Instance, cats, audit, new FixedCatClock(clockToday ?? Today), new FixedIp(), TimeProvider.System), cats, audit);
    }

    private static SterilisationCompletedEvent Event(CatAggregate cat, DateOnly? on = null) => new()
    {
        SurgeryId = Guid.NewGuid(), CatId = cat.Id, BranchId = Guid.NewGuid(), CompletedOnLocalDate = on ?? Today, CorrelationId = Guid.NewGuid(), UserContext = TestUserContext.Create()
    };

    /// <summary>U-E7 -- an intact cat becomes Neutered on the surgery's local date; the vet is the actor.</summary>
    [Fact]
    public async Task U_E7_AnIntactCat_IsRecordedNeutered_WithTheDate()
    {
        var cat = Cat();
        var (handler, cats, audit) = Build(cat);

        await handler.HandleAsync(Event(cat));

        Assert.Equal(NeuterStatus.Neutered, cat.NeuterStatus);
        Assert.Equal(Today, cat.NeuteredOn);
        Assert.Single(cats.Updated);
        Assert.Empty(audit.Saved);
    }

    /// <summary>U-E7 -- already Neutered on the same date: no change, no write (idempotent).</summary>
    [Fact]
    public async Task U_E7_AlreadyNeuteredOnTheSameDate_IsANoOp()
    {
        var cat = Cat();
        var (handler, cats, audit) = Build(cat);
        await handler.HandleAsync(Event(cat));
        var version = cat.Version;
        cats.Updated.Clear();

        await handler.HandleAsync(Event(cat));

        Assert.Equal(version, cat.Version);
        Assert.Empty(cats.Updated);
        Assert.Empty(audit.Saved);
    }

    /// <summary>U-E7 -- a deceased cat: skipped (never an exception), and a NeuterStatusNotApplied audit row is written with the reason.</summary>
    [Fact]
    public async Task U_E7_ADeceasedCat_IsSkipped_WithANeuterStatusNotAppliedAuditRow()
    {
        var cat = Cat();
        cat.MarkDeceased(new CatDeceasedDto(Today, null, Today, TestUserContext.Create(), Guid.NewGuid()));
        var (handler, cats, audit) = Build(cat);
        var surgeryEvent = Event(cat);

        await handler.HandleAsync(surgeryEvent);

        Assert.Equal(NeuterStatus.Unknown, cat.NeuterStatus);
        Assert.Empty(cats.Updated);
        var row = Assert.Single(audit.Saved);
        Assert.Equal("NeuterStatusNotApplied", row.Action);
        Assert.Equal("CatDeceased", row.Reason);
        Assert.Equal(surgeryEvent.SurgeryId, row.EntityId);
        Assert.Equal(AuditCategory.BusinessEvent, row.Category);
    }

    /// <summary>A date HO-CAT-001's own invariant refuses (before the date of birth) is recorded, not thrown.</summary>
    [Fact]
    public async Task ADateTheCatRefuses_IsAuditedNotThrown()
    {
        var cat = Cat();
        var (handler, _, audit) = Build(cat);

        await handler.HandleAsync(Event(cat, new DateOnly(2023, 1, 1))); // before the 2024 date of birth

        Assert.Equal(NeuterStatus.Unknown, cat.NeuterStatus);
        Assert.Equal("NeuterStatusNotApplied", Assert.Single(audit.Saved).Action);
    }

    /// <summary>The branch's local date can be a day ahead of the main branch's: a real procedure date is not refused as "in the future".</summary>
    [Fact]
    public async Task ABranchDateAheadOfTheMainBranch_IsAccepted()
    {
        var cat = Cat();
        var (handler, _, _) = Build(cat, clockToday: Today.AddDays(-1));

        await handler.HandleAsync(Event(cat, Today));

        Assert.Equal(Today, cat.NeuteredOn);
    }
}
