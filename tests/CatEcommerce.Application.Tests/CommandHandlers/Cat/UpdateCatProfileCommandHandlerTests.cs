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

/// <summary>U28, U29 (HO-CAT-001 section 15) -- exercised against UpdateCatProfileCommandHandler,
/// which shares the same "load, mutate, catch" shape as every other handler.</summary>
public sealed class UpdateCatProfileCommandHandlerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);


    // U28
    [Fact]
    public async Task HandleAsync_CatNotFound_ReturnsFailureWithoutThrowing()
    {
        var repository = new FakeAggregateRootRepository<CatAggregate>();
        var handler = new UpdateCatProfileCommandHandler(
            NullLogger<UpdateCatProfileCommandHandler>.Instance, repository, new FakeCatRecordClock(Today));

        var command = new UpdateCatProfileCommand
        {
            CatId = Guid.NewGuid(),
            Name = "Mittens",
            Sex = CatSex.Female,
            DateOfBirth = Today.AddYears(-2),
            DateOfBirthPrecision = DateOfBirthPrecision.Exact,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        };

        var response = await handler.HandleAsync(command);

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.CatNotFound, response.ValidationResult!.Errors[0].ErrorCode);
    }

    // U29
    [Fact]
    public async Task HandleAsync_DomainRuleViolation_IsConvertedToCommandResponseWithCodeAndProperty()
    {
        var repository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, Today.AddYears(-2),
            DateOfBirthPrecision.Exact, null, null, Today, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(cat);

        var handler = new UpdateCatProfileCommandHandler(
            NullLogger<UpdateCatProfileCommandHandler>.Instance, repository, new FakeCatRecordClock(Today));

        var command = new UpdateCatProfileCommand
        {
            CatId = cat.Id,
            Name = "Mittens",
            Sex = CatSex.Female,
            DateOfBirth = Today.AddDays(1), // future -- invalid
            DateOfBirthPrecision = DateOfBirthPrecision.Exact,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        };

        var response = await handler.HandleAsync(command);

        Assert.False(response.IsSuccess);
        var error = response.ValidationResult!.Errors[0];
        Assert.Equal(BusinessViolationCodes.DateOfBirthInFuture, error.ErrorCode);
        Assert.Equal(nameof(UpdateCatProfileCommand.DateOfBirth), error.PropertyName);
        Assert.Empty(repository.Updated); // handler must not persist a failed mutation
    }

    // CODE_REVIEW gate, MEDIUM (2026-09-26): "today" comes from the handler's injected
    // TimeProvider, not the real wall clock -- pinning the clock to a date in the real past
    // still rejects a date of birth "in the future" relative to that pinned clock.
    [Fact]
    public async Task HandleAsync_DateOfBirthAfterPinnedClock_IsRejectedRegardlessOfTheRealClock()
    {
        var pinnedToday = new DateOnly(2020, 1, 1);

        var repository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, pinnedToday.AddYears(-2),
            DateOfBirthPrecision.Exact, null, null, pinnedToday, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(cat);

        var handler = new UpdateCatProfileCommandHandler(
            NullLogger<UpdateCatProfileCommandHandler>.Instance, repository, new FakeCatRecordClock(pinnedToday));

        var command = new UpdateCatProfileCommand
        {
            CatId = cat.Id,
            Name = "Mittens",
            Sex = CatSex.Female,
            DateOfBirth = pinnedToday.AddDays(1), // future relative to the pinned clock only
            DateOfBirthPrecision = DateOfBirthPrecision.Exact,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        };

        var response = await handler.HandleAsync(command);

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.DateOfBirthInFuture, response.ValidationResult!.Errors[0].ErrorCode);
    }
}
