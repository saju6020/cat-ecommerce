using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C4) / US-ACC-05 / ADR-034. Every path returns success with no
/// ValidationResult (R3) -- an account-existence oracle would defeat the point of the neutral
/// response. The token generator runs on every path (even "not found") to remove the largest
/// timing difference; the rate limiter is the real defence (ADR-028), not constant time.
/// </summary>
public sealed class RequestPasswordResetCommandHandler(
    ILogger<RequestPasswordResetCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<PasswordResetTokenAggregate> tokenRepository,
    IPasswordResetTokenGenerator tokenGenerator,
    IPasswordResetTokenDispatcher dispatcher,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RequestPasswordResetCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestPasswordResetCommand command)
    {
        logger.LogInformation("Password reset requested for {AccountKind}. CorrelationId: {CorrelationId}", command.AccountKind, command.CorrelationId);

        var asOf = timeProvider.GetUtcNow();
        var tokenMaterial = tokenGenerator.Generate(asOf);

        AccountIdentifier identifier;
        try
        {
            identifier = AccountIdentifier.Parse(command.Identifier);
        }
        catch (DomainRuleViolationException)
        {
            return Success();
        }

        if (command.AccountKind == AccountKind.Customer)
        {
            var account = await FindAsync(customerRepository, identifier);
            if (account is null || account.Status != CustomerAccountStatus.Active) return Success();

            var channel = ResolveChannel(identifier, account.MobileNumber, account.EmailAddress);
            if (channel is null) return Success();

            var token = new PasswordResetTokenAggregate(command.TokenId);
            token.Issue(new PasswordResetTokenIssueDto(
                AccountKind.Customer, account.Id, tokenMaterial.TokenHash, account.SecurityStamp,
                asOf, tokenMaterial.ExpiresOn, channel, command.UserContext, command.CorrelationId));

            // HO-NTF-001 §11.4 (amended C4): dispatch BEFORE the token's own save -- the
            // dispatcher only enqueues an INotificationOutbox request without saving (F23), so
            // this and the token save below commit in ONE transaction.
            await dispatcher.DispatchAsync(new PasswordResetDispatchRequest(
                AccountKind.Customer, account.Id, channel, MaskDestination(channel, account.MobileNumber, account.EmailAddress),
                tokenMaterial.Token, tokenMaterial.ExpiresOn));

            await tokenRepository.SaveAsync(token, EventPublishOption.InMemoryPublishOnly);
        }
        else
        {
            var account = await FindAsync(staffRepository, identifier);
            if (account is null) return Success();

            var channel = ResolveChannel(identifier, account.MobileNumber, account.EmailAddress);
            if (channel is null) return Success();

            var token = new PasswordResetTokenAggregate(command.TokenId);
            token.Issue(new PasswordResetTokenIssueDto(
                AccountKind.Staff, account.Id, tokenMaterial.TokenHash, account.SecurityStamp,
                asOf, tokenMaterial.ExpiresOn, channel, command.UserContext, command.CorrelationId));

            await dispatcher.DispatchAsync(new PasswordResetDispatchRequest(
                AccountKind.Staff, account.Id, channel, MaskDestination(channel, account.MobileNumber, account.EmailAddress),
                tokenMaterial.Token, tokenMaterial.ExpiresOn));

            await tokenRepository.SaveAsync(token, EventPublishOption.InMemoryPublishOnly);
        }

        return Success();
    }

    private static Task<CustomerAccountAggregate> FindAsync(IAggregateRootRepository<CustomerAccountAggregate> repo, AccountIdentifier identifier) =>
        identifier.Mobile is not null
            ? repo.GetByFilterAsync(c => c.MobileNumber!.Value == identifier.Mobile.Value)
            : repo.GetByFilterAsync(c => c.EmailAddress!.Value == identifier.Email!.Value);

    private static Task<StaffAccountAggregate> FindAsync(IAggregateRootRepository<StaffAccountAggregate> repo, AccountIdentifier identifier) =>
        identifier.Mobile is not null
            ? repo.GetByFilterAsync(c => c.MobileNumber!.Value == identifier.Mobile.Value)
            : repo.GetByFilterAsync(c => c.EmailAddress!.Value == identifier.Email!.Value);

    /// <summary>The identifier the caller typed if it is on the account, else mobile, else email.</summary>
    private static PasswordResetChannel? ResolveChannel(AccountIdentifier identifier, MobileNumber? mobile, EmailAddress? email)
    {
        if (identifier.Mobile is not null && mobile is not null && identifier.Mobile.Value == mobile.Value) return PasswordResetChannel.Mobile;
        if (identifier.Email is not null && email is not null && identifier.Email.Value == email.Value) return PasswordResetChannel.Email;
        if (mobile is not null) return PasswordResetChannel.Mobile;
        if (email is not null) return PasswordResetChannel.Email;
        return null;
    }

    private static string MaskDestination(PasswordResetChannel channel, MobileNumber? mobile, EmailAddress? email) =>
        channel == PasswordResetChannel.Mobile
            ? IdentifierMasking.Mask(mobile!.Value)
            : IdentifierMasking.Mask(email!.Value);

    private static CommandResponse Success() => new() { IsSuccess = true };
}
