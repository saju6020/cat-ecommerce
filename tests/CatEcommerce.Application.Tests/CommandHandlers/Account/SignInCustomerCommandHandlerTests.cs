using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.SharedDto.Account;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15 -- Sign-in & sessions (C).</summary>
public sealed class SignInCustomerCommandHandlerTests
{
    private static CustomerAccountAggregate RegisteredCustomer(FakePasswordHasher hasher, string password = "correctpassword")
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.Register(new CustomerRegistrationDto(
            "Jane Doe", MobileNumber.Create("01712345678"), null, hasher.Hash(password), TestUserContext.Create(), Guid.NewGuid()));
        return customer;
    }

    private static SignInCustomerCommandHandler CreateHandler(
        FakeAggregateRootRepository<CustomerAccountAggregate> customerRepository,
        FakeAggregateRootRepository<CustomerSessionAggregate> sessionRepository,
        FakeAggregateRootRepository<AuditEntryAggregate> auditRepository,
        FakePasswordHasher hasher, FakeTimeProvider timeProvider) =>
        new(NullLogger<SignInCustomerCommandHandler>.Instance, customerRepository, sessionRepository, auditRepository,
            hasher, new FakeSessionTokenIssuer(), new FakeSessionPolicy(), new FakeClientIpAccessor(), timeProvider);

    // U-C5
    [Fact]
    public async Task HandleAsync_UnknownIdentifier_CallsVerifyAgainstDummyAndAuditsMasked()
    {
        var customerRepository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var sessionRepository = new FakeAggregateRootRepository<CustomerSessionAggregate>();
        var auditRepository = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var hasher = new FakePasswordHasher();
        var handler = CreateHandler(customerRepository, sessionRepository, auditRepository, hasher, new FakeTimeProvider());

        var response = await handler.HandleAsync(new SignInCustomerCommand
        {
            SessionId = Guid.NewGuid(), Identifier = "01799999999", Password = "whatever",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(1, hasher.VerifyAgainstDummyCallCount);
        Assert.Single(auditRepository.Saved);
    }

    // U-C6
    [Fact]
    public async Task HandleAsync_WrongPassword_UpdatesAccountButSavesNoSession()
    {
        var customerRepository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var hasher = new FakePasswordHasher();
        var customer = RegisteredCustomer(hasher);
        customerRepository.Seed(customer);

        var sessionRepository = new FakeAggregateRootRepository<CustomerSessionAggregate>();
        var handler = CreateHandler(customerRepository, sessionRepository, new FakeAggregateRootRepository<AuditEntryAggregate>(), hasher, new FakeTimeProvider());

        var response = await handler.HandleAsync(new SignInCustomerCommand
        {
            SessionId = Guid.NewGuid(), Identifier = "01712345678", Password = "wrongpassword",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(customer, customerRepository.Updated);
        Assert.Empty(sessionRepository.Saved);
    }

    // U-C7
    [Fact]
    public async Task HandleAsync_Success_ResetsThrottleAndSavesSessionWithMatchingTokenHash()
    {
        var customerRepository = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var hasher = new FakePasswordHasher();
        var customer = RegisteredCustomer(hasher);
        customerRepository.Seed(customer);

        var sessionRepository = new FakeAggregateRootRepository<CustomerSessionAggregate>();
        var tokenIssuer = new FakeSessionTokenIssuer();
        var handler = new SignInCustomerCommandHandler(
            NullLogger<SignInCustomerCommandHandler>.Instance, customerRepository, sessionRepository,
            new FakeAggregateRootRepository<AuditEntryAggregate>(), hasher, tokenIssuer, new FakeSessionPolicy(),
            new FakeClientIpAccessor(), new FakeTimeProvider());

        var response = await handler.HandleAsync(new SignInCustomerCommand
        {
            SessionId = Guid.NewGuid(), Identifier = "01712345678", Password = "correctpassword",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        var result = (SignInResult)response.Result!;
        Assert.Single(sessionRepository.Saved);
        Assert.Equal(sessionRepository.Saved[0].TokenHash, tokenIssuer.HashToken(result.Token));
        Assert.Equal(0, customer.LoginThrottle.FailedCount);
    }

    // U-G5-equivalent for customer path: staff credentials never checked here (single-repository scope)
    [Fact]
    public void HandleAsync_NeverTouchesAnyRepositoryOtherThanCustomerSessionAndAudit()
    {
        // Structural assertion: the handler's constructor only accepts CustomerAccount/CustomerSession/Audit
        // repositories -- verified by the type signature compiling; no StaffAccountAggregate repository exists.
        var ctorParams = typeof(SignInCustomerCommandHandler).GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(ctorParams, p => p.ParameterType.Name.Contains("StaffAccount"));
    }
}
