using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15 -- Reset (D).</summary>
public sealed class ResetPasswordCommandHandlerTests
{
    private static (FakeAggregateRootRepository<PasswordResetTokenAggregate> Tokens,
        FakeAggregateRootRepository<CustomerAccountAggregate> Customers,
        FakeAggregateRootRepository<StaffAccountAggregate> Staff,
        FakePasswordResetTokenGenerator Generator,
        FakeTimeProvider TimeProvider) Setup()
    {
        return (new(), new(), new(), new(), new());
    }

    // U-D4
    [Fact]
    public async Task HandleAsync_UnknownToken_ReturnsInvalidPasswordResetToken()
    {
        var (tokens, customers, staff, generator, time) = Setup();
        var handler = new ResetPasswordCommandHandler(
            NullLogger<ResetPasswordCommandHandler>.Instance, tokens, customers, staff, generator, new FakePasswordHasher(), new FakeBreachedPasswordChecker(), time);

        var response = await handler.HandleAsync(new ResetPasswordCommand
        {
            Token = "does-not-exist", NewPassword = "longenoughpw", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidPasswordResetToken);
    }

    [Fact]
    public async Task HandleAsync_ExpiredToken_ReturnsInvalidPasswordResetTokenWithSameCode()
    {
        var (tokens, customers, staff, generator, time) = Setup();
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Walk In", MobileNumber.Create("01711111111"), TestUserContext.Create(), Guid.NewGuid()));
        customers.Seed(customer);

        var material = generator.Generate(time.GetUtcNow());
        var token = new PasswordResetTokenAggregate(Guid.NewGuid());
        token.Issue(new PasswordResetTokenIssueDto(
            AccountKind.Customer, customer.Id, material.TokenHash, customer.SecurityStamp,
            time.GetUtcNow(), material.ExpiresOn, PasswordResetChannel.Mobile, TestUserContext.Create(), Guid.NewGuid()));
        tokens.Seed(token);

        time.Advance(TimeSpan.FromMinutes(16));

        var handler = new ResetPasswordCommandHandler(
            NullLogger<ResetPasswordCommandHandler>.Instance, tokens, customers, staff, generator, new FakePasswordHasher(), new FakeBreachedPasswordChecker(), time);

        var response = await handler.HandleAsync(new ResetPasswordCommand
        {
            Token = material.Token, NewPassword = "longenoughpw", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidPasswordResetToken);
    }

    // U-D5
    [Fact]
    public async Task HandleAsync_BreachedNewPassword_DoesNotConsumeToken()
    {
        var (tokens, customers, staff, generator, time) = Setup();
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Walk In", MobileNumber.Create("01711111111"), TestUserContext.Create(), Guid.NewGuid()));
        customers.Seed(customer);

        var material = generator.Generate(time.GetUtcNow());
        var token = new PasswordResetTokenAggregate(Guid.NewGuid());
        token.Issue(new PasswordResetTokenIssueDto(
            AccountKind.Customer, customer.Id, material.TokenHash, customer.SecurityStamp,
            time.GetUtcNow(), material.ExpiresOn, PasswordResetChannel.Mobile, TestUserContext.Create(), Guid.NewGuid()));
        tokens.Seed(token);

        var stampBefore = customer.SecurityStamp;
        var handler = new ResetPasswordCommandHandler(
            NullLogger<ResetPasswordCommandHandler>.Instance, tokens, customers, staff, generator, new FakePasswordHasher(), new FakeBreachedPasswordChecker(isBreached: true), time);

        var response = await handler.HandleAsync(new ResetPasswordCommand
        {
            Token = material.Token, NewPassword = "password123", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.BreachedPassword);
        Assert.Equal(stampBefore, customer.SecurityStamp); // token still usable -- stamp unchanged
    }

    [Fact]
    public async Task HandleAsync_ValidToken_SetsPasswordAndRotatesStamp()
    {
        var (tokens, customers, staff, generator, time) = Setup();
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Walk In", MobileNumber.Create("01711111111"), TestUserContext.Create(), Guid.NewGuid()));
        customers.Seed(customer);

        var material = generator.Generate(time.GetUtcNow());
        var token = new PasswordResetTokenAggregate(Guid.NewGuid());
        token.Issue(new PasswordResetTokenIssueDto(
            AccountKind.Customer, customer.Id, material.TokenHash, customer.SecurityStamp,
            time.GetUtcNow(), material.ExpiresOn, PasswordResetChannel.Mobile, TestUserContext.Create(), Guid.NewGuid()));
        tokens.Seed(token);

        var stampBefore = customer.SecurityStamp;
        var handler = new ResetPasswordCommandHandler(
            NullLogger<ResetPasswordCommandHandler>.Instance, tokens, customers, staff, generator, new FakePasswordHasher(), new FakeBreachedPasswordChecker(), time);

        var response = await handler.HandleAsync(new ResetPasswordCommand
        {
            Token = material.Token, NewPassword = "longenoughpw", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.NotEqual(stampBefore, customer.SecurityStamp);
        Assert.NotNull(customer.PasswordCredential);
    }
}
