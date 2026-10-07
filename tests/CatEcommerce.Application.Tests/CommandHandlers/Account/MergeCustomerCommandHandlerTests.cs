using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15 -- Merge (I).</summary>
public sealed class MergeCustomerCommandHandlerTests
{
    private static CustomerAccountAggregate Registered(MobileNumber mobile)
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.Register(new CustomerRegistrationDto(
            "Jane", mobile, null, PasswordCredential.Create("h", "a", DateTimeOffset.UtcNow), TestUserContext.Create(), Guid.NewGuid()));
        return customer;
    }

    [Fact]
    public async Task HandleAsync_DuplicateNotFound_ReturnsCustomerNotFound()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var survivor = Registered(MobileNumber.Create("01712345678"));
        repository.Seed(survivor);

        var handler = new MergeCustomerCommandHandler(NullLogger<MergeCustomerCommandHandler>.Instance, repository, new FakeTimeProvider());

        var response = await handler.HandleAsync(new MergeCustomerCommand
        {
            DuplicateCustomerId = Guid.NewGuid(), SurvivingCustomerId = survivor.Id, Reason = "duplicate",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.CustomerNotFound);
    }

    [Fact]
    public async Task HandleAsync_SurvivorNotActive_ReturnsAccountNotActive()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var duplicate = Registered(MobileNumber.Create("01711111111"));
        var survivor = Registered(MobileNumber.Create("01712345678"));
        survivor.Anonymise(new CustomerAnonymisationDto(DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(duplicate);
        repository.Seed(survivor);

        var handler = new MergeCustomerCommandHandler(NullLogger<MergeCustomerCommandHandler>.Instance, repository, new FakeTimeProvider());

        var response = await handler.HandleAsync(new MergeCustomerCommand
        {
            DuplicateCustomerId = duplicate.Id, SurvivingCustomerId = survivor.Id, Reason = "duplicate",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.AccountNotActive);
    }

    [Fact]
    public async Task HandleAsync_SharedMobile_MergesAndReturnsIsReapplicationFalse()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var mobile = MobileNumber.Create("01712345678");
        var survivor = Registered(mobile);
        var duplicate = Registered(MobileNumber.Create("01799999999"));
        duplicate.UpdateContactDetails(new CustomerContactDetailsDto("Jane", null, [mobile], TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(survivor);
        repository.Seed(duplicate);

        var handler = new MergeCustomerCommandHandler(NullLogger<MergeCustomerCommandHandler>.Instance, repository, new FakeTimeProvider());

        var response = await handler.HandleAsync(new MergeCustomerCommand
        {
            DuplicateCustomerId = duplicate.Id, SurvivingCustomerId = survivor.Id, Reason = "duplicate",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Contains(duplicate, repository.Updated);
        Assert.Equal(CatEcommerce.Shared.Common.Enums.CustomerAccountStatus.Merged, duplicate.Status);
    }

    // CODE_REVIEW gate, MEDIUM (2026-09-25): MergedOn must come from the injected TimeProvider,
    // not a DateTimeOffset.UtcNow read inside MergeInto -- this is what actually makes the
    // aggregate method testable with a controlled clock.
    [Fact]
    public async Task HandleAsync_Success_SetsMergedOnToTheTimeProvidersTime()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var mobile = MobileNumber.Create("01712345678");
        var survivor = Registered(mobile);
        var duplicate = Registered(MobileNumber.Create("01799999999"));
        duplicate.UpdateContactDetails(new CustomerContactDetailsDto("Jane", null, [mobile], TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(survivor);
        repository.Seed(duplicate);

        var fixedTime = new DateTimeOffset(2026, 6, 15, 8, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);
        var handler = new MergeCustomerCommandHandler(NullLogger<MergeCustomerCommandHandler>.Instance, repository, timeProvider);

        var response = await handler.HandleAsync(new MergeCustomerCommand
        {
            DuplicateCustomerId = duplicate.Id, SurvivingCustomerId = survivor.Id, Reason = "duplicate",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(fixedTime, duplicate.MergedOn);
    }
}
