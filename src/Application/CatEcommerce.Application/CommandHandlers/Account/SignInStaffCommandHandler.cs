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
/// HO-ACC-001 §11.4 (C12) / US-ACC-14. The customer repository is never queried here -- a
/// customer credential structurally cannot yield a staff session (U-G5, FR-ACC-3).
/// </summary>
public sealed class SignInStaffCommandHandler(
    ILogger<SignInStaffCommandHandler> logger,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<StaffSessionAggregate> sessionRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IPasswordHasher passwordHasher,
    ISessionTokenIssuer sessionTokenIssuer,
    ISessionPolicy sessionPolicy,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SignInStaffCommand>
{
    public async Task<CommandResponse> HandleAsync(SignInStaffCommand command)
    {
        logger.LogInformation("Staff sign-in attempt. CorrelationId: {CorrelationId}", command.CorrelationId);

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
            ? await staffRepository.GetByFilterAsync(c => c.MobileNumber!.Value == identifier.Mobile.Value)
            : await staffRepository.GetByFilterAsync(c => c.EmailAddress!.Value == identifier.Email!.Value);

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
            // SECURITY gate MEDIUM / ADR-028: see SignInCustomerCommandHandler's identical comment
            // -- a locked, not-active or no-password staff account must cost the same one Argon2
            // verify as a wrong password on a real account.
            passwordHasher.VerifyAgainstDummy(command.Password);
            await WriteAuditAsync(account.Id, "StaffSignInRefused", ex.ErrorCode, command.CorrelationId, command.UserContext);
            return InvalidCredentials();
        }

        if (!passwordHasher.Verify(command.Password, account.PasswordCredential!))
        {
            account.RecordFailedLogin(new LoginFailureDto(asOf, command.UserContext, command.CorrelationId));
            await staffRepository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);
            return InvalidCredentials();
        }

        account.RecordSuccessfulLogin(new LoginSuccessDto(asOf, command.UserContext, command.CorrelationId));
        await staffRepository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);

        var tokenMaterial = sessionTokenIssuer.Issue();
        // SECURITY gate HIGH (second pass) / ADR-342 (owner decision A): see
        // SignInCustomerCommandHandler's identical comment.
        var csrfMaterial = sessionTokenIssuer.Issue();
        var session = new StaffSessionAggregate(command.SessionId);
        session.Issue(new SessionIssueDto(
            account.Id, tokenMaterial.TokenHash, account.SecurityStamp, asOf,
            asOf.Add(sessionPolicy.StaffSessionLifetime),
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
            EntityType: "StaffAccount", EntityId: Guid.Empty, Action: "StaffSignInFailed",
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
            EntityType: "StaffAccount", EntityId: accountId, Action: action, Reason: reason,
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
            errorCode: BusinessViolationCodes.InvalidCredentials, resourceName: nameof(StaffAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
