using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;

namespace CatEcommerce.Domain.Tests.Aggregates;

public sealed class CatWeightRecordAggregateTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void Record_Valid_SetsWeightAndRaisesEvent()
    {
        var record = new CatWeightRecordAggregate(Guid.NewGuid());
        var catId = Guid.NewGuid();

        record.Record(new CatWeightRecordDto(catId, 4.2m, Today, Guid.NewGuid(), null, Today,
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(catId, record.CatId);
        Assert.Equal(4.2m, record.Weight.Kilograms);
        Assert.Single(record.Events);
    }

    [Fact]
    public void Record_FutureDate_Throws()
    {
        var record = new CatWeightRecordAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() => record.Record(
            new CatWeightRecordDto(Guid.NewGuid(), 4.2m, Today.AddDays(1), Guid.NewGuid(), null, Today,
                TestUserContext.Create(), Guid.NewGuid())));
    }

    // CODE_REVIEW gate, MEDIUM (2026-09-26): RecordedOn is only "in the future" relative to the
    // Today threaded in from the caller's clock, not the real wall clock -- this is what makes
    // the boundary pinnable with a fixed Today, independent of when the test actually runs.
    [Fact]
    public void Record_DateEqualToThreadedToday_Succeeds()
    {
        var record = new CatWeightRecordAggregate(Guid.NewGuid());
        var pinnedToday = new DateOnly(2026, 6, 15);

        record.Record(new CatWeightRecordDto(Guid.NewGuid(), 4.2m, pinnedToday, Guid.NewGuid(), null, pinnedToday,
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(pinnedToday, record.RecordedOn);
    }

    [Fact]
    public void Record_DateAfterThreadedToday_ThrowsEvenIfNotAfterTheRealClock()
    {
        var record = new CatWeightRecordAggregate(Guid.NewGuid());
        var pinnedToday = new DateOnly(2020, 1, 1); // in the real past, but "today" for this call

        Assert.Throws<DomainRuleViolationException>(() => record.Record(
            new CatWeightRecordDto(Guid.NewGuid(), 4.2m, pinnedToday.AddDays(1), Guid.NewGuid(), null, pinnedToday,
                TestUserContext.Create(), Guid.NewGuid())));
    }
}
