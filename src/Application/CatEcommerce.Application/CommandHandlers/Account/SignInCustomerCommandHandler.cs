using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using CatEcommerce.Shared.SharedDto.Account;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C2) / US-ACC-04 / ADR-028 / ADR-033. Sign-in touches two aggregates (the
/// account's throttle and a new session) in one handler -- a documented exception to
/// `.ai/bounded-contexts.md` rule 1.
/// </summary>
public sealed class SignInCustomerCommandHandler(
    ILogger<SignInCustomerCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<CustomerSessionAggregate> sessionRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IPasswordHasher passwordHasher,
    ISessionTokenIssuer sessionTokenIssuer,
    ISessionPolicy sessionPolicy,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SignInCustomerCommand>
{
    public async Task<CommandResponse> HandleAsync(SignInCustomerCommand command)
    {
        logger.LogInformation("Customer sign-in attempt. CorrelationId: {CorrelationId}", command.CorrelationId);

        var asOf = timeProvider.GetUtcNow();

        AccountIdentifier identifier;
        try
        {
            identifier = AccountIdentifier.Parse(command.Identifier);
        }
        catch (DomainRuleViolationException)
        {
            return InvalidCredentials();
        }

        var account = identifier.Mobile is not null
            ? await customerRepository.GetByFilterAsync(c => c.MobileNumber!.Value == identifier.Mobile.Value)
            : await customerRepository.GetByFilterAsync(c => c.EmailAddress!.Value == identifier.Email!.Value);

        if (account is null)
        {
            passwordHasher.VerifyAgainstDummy(command.Password);
            await WriteUnknownIdentifierAuditAsync(command.Identifier, command.CorrelationId, command.UserContext);
            return InvalidCredentials();
        }

        try
        {
            account.EnsureCanSignIn(asOf);
        }
        catch (DomainRuleViolationException ex)
        {
            // SECURITY gate MEDIUM / ADR-028: a locked, not-active or no-password account must
            // cost exactly the same one Argon2 verify as a wrong password on a real account --
            // otherwise this refusal returns far faster than the wrong-password path below, and
            // that timing gap is itself an account-state oracle R1 exists to remove.
            passwordHasher.VerifyAgainstDummy(command.Password);
            await WriteAuditAsync(account.Id, "CustomerSignInRefused", ex.ErrorCode, command.CorrelationId, command.UserContext);
            return InvalidCredentials();
        }

        if (!passwordHasher.Verify(command.Password, account.PasswordCredential!))
        {
            account.RecordFailedLogin(new LoginFailureDto(asOf, command.UserContext, command.CorrelationId));
            await customerRepository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);
            return InvalidCredentials();
        }

        account.RecordSuccessfulLogin(new LoginSuccessDto(asOf, command.UserContext, command.CorrelationId));
        await customerRepository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);

        var tokenMaterial = sessionTokenIssuer.Issue();
        // SECURITY gate HIGH (second pass) / ADR-342 (owner decision A): a second, independent
        // token+hash pair for the CSRF cookie, generated the same way as the session token itself
        // and stored on the session row -- not derived from a process-lifetime key, so it survives
        // a restart exactly like the session it protects.
        var csrfMaterial = sessionTokenIssuer.Issue();
        var session = new CustomerSessionAggregate(command.SessionId);
        session.Issue(new SessionIssueDto(
            account.Id, tokenMaterial.TokenHash, account.SecurityStamp, asOf,
            asOf.Add(sessionPolicy.CustomerSessionLifetime),
            command.IpAddress, command.UserAgent, command.UserContext, command.CorrelationId,
            csrfMaterial.TokenHash));

        await sessionRepository.SaveAsync(session, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse
        {
            IsSuccess = true,
            Result = new SignInResult(tokenMaterial.Token, session.ExpiresOn, account.Id, csrfMaterial.Token)
        };
    }

    private async Task WriteUnknownIdentifierAuditAsync(string identifier, Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "CustomerAccount", EntityId: Guid.Empty, Action: "CustomerSignInFailed",
            Reason: "UnknownIdentifier", ActorUserId: Guid.Empty, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: IdentifierMasking.Mask(identifier), AfterValue: null,
            UserContext: userContext, CorrelationId: correlationId,
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }

    private async Task WriteAuditAsync(Guid accountId, string action, string reason, Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "CustomerAccount", EntityId: accountId, Action: action, Reason: reason,
            ActorUserId: accountId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null, AfterValue: null,
            UserContext: userContext, CorrelationId: correlationId,
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }

    private static CommandResponse InvalidCredentials()
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(
            errorMessage: AccountMessages.SignInFailed, propertyName: "Identifier",
            errorCode: BusinessViolationCodes.InvalidCredentials, resourceName: nameof(CustomerAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
