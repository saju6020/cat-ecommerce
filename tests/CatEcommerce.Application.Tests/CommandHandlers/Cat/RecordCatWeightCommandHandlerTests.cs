using CatEcommerce.Application.CommandHandlers.Cat;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Cat;

public sealed class RecordCatWeightCommandHandlerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);


    // U32
    [Fact]
    public async Task HandleAsync_Valid_SavesWeightRecordAndDoesNotSaveCat()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, Today.AddYears(-2),
            DateOfBirthPrecision.Exact, null, null, Today, TestUserContext.Create(), Guid.NewGuid()));
        catRepository.Seed(cat);

        var weightRepository = new FakeAggregateRootRepository<CatWeightRecordAggregate>();
        var handler = new RecordCatWeightCommandHandler(
            NullLogger<RecordCatWeightCommandHandler>.Instance, catRepository, weightRepository,
            new FakeCatRecordClock(Today));

        var response = await handler.HandleAsync(new RecordCatWeightCommand
        {
            WeightRecordId = Guid.NewGuid(),
            CatId = cat.Id,
            Kilograms = 4.2m,
            RecordedOn = Today,
            RecordedByStaffId = Guid.NewGuid(),
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Single(weightRepository.Saved);
        Assert.Empty(catRepository.Saved);
        Assert.Empty(catRepository.Updated);
    }

    // CODE_REVIEW gate, MEDIUM (2026-09-26): RecordedOn "in the future" is evaluated against the
    // handler's injected TimeProvider, not the real wall clock.
    [Fact]
    public async Task HandleAsync_RecordedOnAfterPinnedClock_ReturnsWeightRecordDateInFuture()
    {
        var pinnedToday = new DateOnly(2020, 1, 1);

        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, pinnedToday.AddYears(-2),
            DateOfBirthPrecision.Exact, null, null, pinnedToday, TestUserContext.Create(), Guid.NewGuid()));
        catRepository.Seed(cat);

        var weightRepository = new FakeAggregateRootRepository<CatWeightRecordAggregate>();
        var handler = new RecordCatWeightCommandHandler(
            NullLogger<RecordCatWeightCommandHandler>.Instance, catRepository, weightRepository,
            new FakeCatRecordClock(pinnedToday));

        var response = await handler.HandleAsync(new RecordCatWeightCommand
        {
            WeightRecordId = Guid.NewGuid(),
            CatId = cat.Id,
            Kilograms = 4.2m,
            RecordedOn = pinnedToday.AddDays(1), // future relative to the pinned clock only
            RecordedByStaffId = Guid.NewGuid(),
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.WeightRecordDateInFuture, response.ValidationResult!.Errors[0].ErrorCode);
    }
}
