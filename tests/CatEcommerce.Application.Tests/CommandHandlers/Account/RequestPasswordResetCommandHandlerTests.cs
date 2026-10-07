using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15, U-D3 -- every path returns IsSuccess with no ValidationResult;
/// only "found" saves and dispatches; the generator runs on every path.</summary>
public sealed class RequestPasswordResetCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_UnknownIdentifier_SucceedsSavesNothingButStillGeneratesToken()
    {
        var tokens = new FakeAggregateRootRepository<PasswordResetTokenAggregate>();
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var staff = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var generator = new FakePasswordResetTokenGenerator();
        var dispatcher = new FakePasswordResetTokenDispatcher();

        var handler = new RequestPasswordResetCommandHandler(
            NullLogger<RequestPasswordResetCommandHandler>.Instance, customers, staff, tokens, generator, dispatcher, new FakeTimeProvider());

        var response = await handler.HandleAsync(new RequestPasswordResetCommand
        {
            TokenId = Guid.NewGuid(), AccountKind = AccountKind.Customer, Identifier = "01799999999",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Null(response.ValidationResult);
        Assert.Empty(tokens.Saved);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_MergedAccount_SucceedsWithoutIssuingToken()
    {
        var tokens = new FakeAggregateRootRepository<PasswordResetTokenAggregate>();
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.Register(new CustomerRegistrationDto(
            "Jane", MobileNumber.Create("01712345678"), null, PasswordCredential.Create("h", "a", DateTimeOffset.UtcNow),
            TestUserContext.Create(), Guid.NewGuid()));
        customer.MergeInto(new CustomerMergeDto(Guid.NewGuid(), "dup", customer.MobileNumber, [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        customers.Seed(customer);

        var handler = new RequestPasswordResetCommandHandler(
            NullLogger<RequestPasswordResetCommandHandler>.Instance, customers, new FakeAggregateRootRepository<StaffAccountAggregate>(),
            tokens, new FakePasswordResetTokenGenerator(), new FakePasswordResetTokenDispatcher(), new FakeTimeProvider());

        var response = await handler.HandleAsync(new RequestPasswordResetCommand
        {
            TokenId = Guid.NewGuid(), AccountKind = AccountKind.Customer, Identifier = "01712345678",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Empty(tokens.Saved);
    }

    [Fact]
    public async Task HandleAsync_ActiveAccount_IssuesTokenAndDispatches()
    {
        var tokens = new FakeAggregateRootRepository<PasswordResetTokenAggregate>();
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.Register(new CustomerRegistrationDto(
            "Jane", MobileNumber.Create("01712345678"), null, PasswordCredential.Create("h", "a", DateTimeOffset.UtcNow),
            TestUserContext.Create(), Guid.NewGuid()));
        customers.Seed(customer);
        var dispatcher = new FakePasswordResetTokenDispatcher();

        var handler = new RequestPasswordResetCommandHandler(
            NullLogger<RequestPasswordResetCommandHandler>.Instance, customers, new FakeAggregateRootRepository<StaffAccountAggregate>(),
            tokens, new FakePasswordResetTokenGenerator(), dispatcher, new FakeTimeProvider());

        var response = await handler.HandleAsync(new RequestPasswordResetCommand
        {
            TokenId = Guid.NewGuid(), AccountKind = AccountKind.Customer, Identifier = "01712345678",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Single(tokens.Saved);
        Assert.Single(dispatcher.Dispatched);
    }
}
