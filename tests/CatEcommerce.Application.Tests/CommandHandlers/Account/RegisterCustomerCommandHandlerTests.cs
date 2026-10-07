using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15 -- Registration (B).</summary>
public sealed class RegisterCustomerCommandHandlerTests
{
    private static RegisterCustomerCommandHandler CreateHandler(
        FakeAggregateRootRepository<CustomerAccountAggregate> repository,
        FakePasswordHasher hasher, FakeBreachedPasswordChecker? breachChecker = null) =>
        new(NullLogger<RegisterCustomerCommandHandler>.Instance, repository, hasher, breachChecker ?? new FakeBreachedPasswordChecker());

    // U-B6
    [Fact]
    public async Task HandleAsync_MobileAlreadyRegistered_ReturnsViolationAndNeverCallsHasher()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var existing = new CustomerAccountAggregate(Guid.NewGuid());
        existing.Register(new CustomerRegistrationDto(
            "Existing", MobileNumber.Create("01712345678"), null,
            PasswordCredential.Create("h", "a", DateTimeOffset.UtcNow), TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(existing);

        var hasher = new FakePasswordHasher();
        var handler = CreateHandler(repository, hasher);

        var response = await handler.HandleAsync(new RegisterCustomerCommand
        {
            CustomerId = Guid.NewGuid(), FullName = "New Person", MobileNumber = "01712345678",
            Password = "longenoughpw", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.MobileNumberAlreadyRegistered);
        Assert.DoesNotContain(response.ValidationResult.Errors, e => e.ErrorMessage.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.Null(hasher.LastPlaintextHashed);
    }

    // U-B7
    [Fact]
    public async Task HandleAsync_EmailAlreadyRegistered_MessageMentionsNoMobileOrPhone()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var existing = new CustomerAccountAggregate(Guid.NewGuid());
        existing.Register(new CustomerRegistrationDto(
            "Existing", null, EmailAddress.Create("taken@example.com"),
            PasswordCredential.Create("h", "a", DateTimeOffset.UtcNow), TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(existing);

        var handler = CreateHandler(repository, new FakePasswordHasher());

        var response = await handler.HandleAsync(new RegisterCustomerCommand
        {
            CustomerId = Guid.NewGuid(), FullName = "New Person", EmailAddress = "taken@example.com",
            Password = "longenoughpw", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.EmailAddressAlreadyRegistered);
        Assert.DoesNotContain(response.ValidationResult.Errors, e =>
            e.ErrorMessage.Contains("mobile", StringComparison.OrdinalIgnoreCase) || e.ErrorMessage.Contains("phone", StringComparison.OrdinalIgnoreCase));
    }

    // U-B8
    [Fact]
    public async Task HandleAsync_BreachedPassword_ReturnsViolationBeforeAnyWrite()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var handler = CreateHandler(repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker(isBreached: true));

        var response = await handler.HandleAsync(new RegisterCustomerCommand
        {
            CustomerId = Guid.NewGuid(), FullName = "New Person", MobileNumber = "01712345678",
            Password = "password123", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.BreachedPassword);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task HandleAsync_Valid_SavesCustomerAndReturnsId()
    {
        var repository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var handler = CreateHandler(repository, new FakePasswordHasher());
        var customerId = Guid.NewGuid();

        var response = await handler.HandleAsync(new RegisterCustomerCommand
        {
            CustomerId = customerId, FullName = "New Person", MobileNumber = "01712345678",
            Password = "longenoughpw", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(customerId, response.Result);
        Assert.Single(repository.Saved);
    }
}
