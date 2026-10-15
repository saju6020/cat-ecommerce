using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>
/// HO-PHM-001 §8.9 / §12.2 (ADR-146, BR-7) -- verifies a controlled-drug WITNESS: a person, not a dropdown. The witness enters their OWN identifier and
/// password in the same request; this reuses HO-ACC-001's identifier parsing, staff lookup, Argon2id hasher and progressive login throttle (V3) and creates NO
/// session. Verified only if the account exists, can sign in right now (not locked, has a password), the password verifies, the account holds a role in
/// <see cref="ControlledDrugRules.WitnessRoles"/>, and it is not the person acting.
///
/// <b>Every failure is the same result</b> -- never which part failed -- and costs one Argon2 verify (the dummy verify when no real credential is checked), so
/// neither the answer nor the response time tells a guesser anything. A wrong password counts toward the witness account's lockout EXACTLY as a sign-in
/// failure does (<c>RecordFailedLogin</c>) and writes an <c>AuthenticationEvent</c> audit row <c>WitnessVerificationFailed</c> with the MASKED identifier; success writes
/// <c>WitnessVerified</c> with the witness id and the source. The password is never logged, audited, put in an event or enqueued.
/// </summary>
public sealed class StaffWitnessVerifier(
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IPasswordHasher passwordHasher,
    IClientIpAccessor clientIpAccessor,
    IUserContextProvider userContextProvider,
    TimeProvider timeProvider) : IStaffWitnessVerifier
{
    private static readonly WitnessVerification NotVerified = new(false, null);

    public async Task<WitnessVerification> VerifyAsync(
        string identifier, string password, Guid actingStaffId, ControlledDrugSourceType source, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var userContext = userContextProvider.GetUserContext();

        StaffAccountAggregate? account = null;
        try
        {
            var parsed = AccountIdentifier.Parse(identifier ?? string.Empty);
            account = parsed.Mobile is not null
                ? await staffRepository.GetByFilterAsync(s => s.MobileNumber!.Value == parsed.Mobile.Value)
                : await staffRepository.GetByFilterAsync(s => s.EmailAddress!.Value == parsed.Email!.Value);
        }
        catch (DomainRuleViolationException)
        {
            // An unparseable identifier is an unknown identifier.
        }

        if (account is null)
        {
            passwordHasher.VerifyAgainstDummy(password ?? string.Empty);
            await AuditFailureAsync(Guid.Empty, identifier, source, userContext, now);
            return NotVerified;
        }

        try
        {
            account.EnsureCanSignIn(now);
        }
        catch (DomainRuleViolationException)
        {
            passwordHasher.VerifyAgainstDummy(password ?? string.Empty);
            await AuditFailureAsync(account.Id, identifier, source, userContext, now);
            return NotVerified;
        }

        if (!passwordHasher.Verify(password ?? string.Empty, account.PasswordCredential!))
        {
            // Counts toward THIS account's lockout exactly as a failed sign-in does.
            account.RecordFailedLogin(new LoginFailureDto(now, userContext, Guid.NewGuid()));
            await staffRepository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);
            await AuditFailureAsync(account.Id, identifier, source, userContext, now);
            return NotVerified;
        }

        // The password was right, so this is not a guess: the throttle is untouched. The refusals below are the same generic result.
        if (account.Id == actingStaffId || !account.RoleIds.Any(role => ControlledDrugRules.WitnessRoles.Contains(role)))
        {
            await AuditFailureAsync(account.Id, identifier, source, userContext, now);
            return NotVerified;
        }

        await WriteAuditAsync("WitnessVerified", account.Id, $"Source={source.DisplayName};WitnessId={account.Id}", userContext, now);
        return new WitnessVerification(true, account.Id);
    }

    private Task AuditFailureAsync(Guid accountId, string? identifier, ControlledDrugSourceType source, UserContext userContext, DateTimeOffset now) =>
        WriteAuditAsync("WitnessVerificationFailed", accountId, $"Source={source.DisplayName};Identifier={IdentifierMasking.Mask(identifier ?? string.Empty)}", userContext, now);

    private async Task WriteAuditAsync(string action, Guid accountId, string afterValue, UserContext userContext, DateTimeOffset now)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "StaffAccount", EntityId: accountId, Action: action, Reason: action, ActorUserId: userContext.UserId, OccurredOn: now,
            BeforeValue: null, AfterValue: afterValue, UserContext: userContext, CorrelationId: Guid.NewGuid(),
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
