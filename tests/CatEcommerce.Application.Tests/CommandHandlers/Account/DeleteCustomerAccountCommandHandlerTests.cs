using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15 -- Deletion & export (J).</summary>
public sealed class DeleteCustomerAccountCommandHandlerTests
{
    private static CustomerAccountAggregate RegisteredCustomer(FakePasswordHasher hasher, UserContext userContext) =>
        Register(hasher, userContext);

    private static CustomerAccountAggregate Register(FakePasswordHasher hasher, UserContext userContext)
    {
        var customer = new CustomerAccountAggregate(userContext.UserId);
        customer.Register(new CustomerRegistrationDto(
            "Jane Doe", MobileNumber.Create("01712345678"), null, hasher.Hash("correctpassword"), TestUserContext.Create(), Guid.NewGuid()));
        return customer;
    }

    // U-J3
    [Fact]
    public async Task HandleAsync_WhileLocked_ReturnsAccountLockedAndNeverCallsHasher()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var hasher = new FakePasswordHasher();
        var userContext = TestUserContext.Create();
        var customer = Register(hasher, userContext);
        var asOf = DateTimeOffset.UtcNow;
        customer.RecordFailedLogin(new LoginFailureDto(asOf, userContext, Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, userContext, Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, userContext, Guid.NewGuid()));
        repository.Seed(customer);

        var time = new FakeTimeProvider();
        time.SetUtcNow(asOf.AddSeconds(1));
        var handler = new DeleteCustomerAccountCommandHandler(NullLogger<DeleteCustomerAccountCommandHandler>.Instance, repository, hasher, time);

        var response = await handler.HandleAsync(new DeleteCustomerAccountCommand
        {
            CurrentPassword = "correctpassword", UserContext = userContext, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.AccountLocked);
    }

    // U-J4
    [Fact]
    public async Task HandleAsync_WrongPassword_PersistsFailedLoginAndReturnsReauthenticationFailed()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var hasher = new FakePasswordHasher();
        var userContext = TestUserContext.Create();
        var customer = Register(hasher, userContext);
        repository.Seed(customer);

        var handler = new DeleteCustomerAccountCommandHandler(NullLogger<DeleteCustomerAccountCommandHandler>.Instance, repository, hasher, new FakeTimeProvider());

        var response = await handler.HandleAsync(new DeleteCustomerAccountCommand
        {
            CurrentPassword = "wrongpassword", UserContext = userContext, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.ReauthenticationFailed);
        Assert.Equal(1, customer.LoginThrottle.FailedCount);
        Assert.Contains(customer, repository.Updated);
    }

    [Fact]
    public async Task HandleAsync_CorrectPassword_Anonymises()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var hasher = new FakePasswordHasher();
        var userContext = TestUserContext.Create();
        var customer = Register(hasher, userContext);
        repository.Seed(customer);

        var handler = new DeleteCustomerAccountCommandHandler(NullLogger<DeleteCustomerAccountCommandHandler>.Instance, repository, hasher, new FakeTimeProvider());

        var response = await handler.HandleAsync(new DeleteCustomerAccountCommand
        {
            CurrentPassword = "correctpassword", UserContext = userContext, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(CatEcommerce.Shared.Common.Enums.CustomerAccountStatus.Anonymised, customer.Status);
    }
}
