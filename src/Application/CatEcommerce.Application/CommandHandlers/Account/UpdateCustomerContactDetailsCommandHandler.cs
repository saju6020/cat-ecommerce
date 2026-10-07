using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C6) / US-ACC-06. The customer id comes from <c>UserContext.UserId</c>, never
/// the request body (§11.1).
///
/// SECURITY gate LOW (HO-ACC-001 §1 Amended, self-amendment to §8.3): changing the email requires
/// <c>CurrentPassword</c>, the same step-up C18 (account deletion) already requires, whenever the
/// email is actually changing (added, removed or modified) -- email is also a login and reset
/// identifier, so a stolen session alone must not be enough to redirect password resets to an
/// attacker-controlled address and turn a temporary session theft into a permanent takeover.
///
/// SECURITY gate MEDIUM (second pass, HO-ACC-001 §1 Amended (18)): this step-up must be exactly as
/// throttled as C18 deletion's -- refuse when the account is already locked (BEFORE verifying, so
/// the lockout window itself cannot be probed) and call <c>RecordFailedLogin</c> on a mismatch, so
/// an attacker with a stolen session cannot run an unbounded dictionary attack against the
/// victim's password through this endpoint. <c>CustomerProfileController</c> additionally applies
/// <c>RateLimitPolicies.AccountSensitive</c> to bound concurrent Argon2id verifies.
///
/// SECURITY gate LOW (second pass, HO-ACC-001 §1 Amended (18)): a NEWLY added additional number
/// that equals another ACTIVE customer's PRIMARY mobile is refused here -- a repository existence
/// check, per ADR-015 (cross-aggregate uniqueness lives in the handler, not the aggregate). Only
/// numbers not already present on this account are checked, so a number accepted before this fix
/// shipped does not retroactively block an unrelated edit (a name change, say) on the same
/// account. This does not touch RegisterWalkInCustomerCommandHandler's own PRIMARY-vs-PRIMARY
/// refusal (C15), which is unchanged (§1 Amended, 2026-09-25).
/// </summary>
public sealed class UpdateCustomerContactDetailsCommandHandler(
    ILogger<UpdateCustomerContactDetailsCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UpdateCustomerContactDetailsCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateCustomerContactDetailsCommand command)
    {
        logger.LogInformation("Updating customer contact details. CorrelationId: {CorrelationId}", command.CorrelationId);

        var customer = await customerRepository.GetByIdAsync(command.UserContext.UserId);
        if (customer is null) return Violation(BusinessViolationCodes.CustomerNotFound, "CustomerId", "This account was not found.");

        EmailAddress? email = null;
        List<MobileNumber> additional = [];

        try
        {
            if (!string.IsNullOrWhiteSpace(command.EmailAddress)) email = EmailAddress.Create(command.EmailAddress);
            foreach (var number in command.AdditionalPhoneNumbers) additional.Add(MobileNumber.Create(number));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var newlyAddedNumbers = additional.Select(n => n.Value).Except(customer.AdditionalPhoneNumbers).ToList();
        if (newlyAddedNumbers.Count > 0)
        {
            var claimsAnotherCustomersPrimary = await customerRepository.ExistAsync(c =>
                c.Id != customer.Id && c.Status == CustomerAccountStatus.Active &&
                c.MobileNumber != null && newlyAddedNumbers.Contains(c.MobileNumber.Value));

            if (claimsAnotherCustomersPrimary)
                return Violation(
                    BusinessViolationCodes.AdditionalPhoneNumberMatchesAnotherCustomer,
                    "AdditionalPhoneNumbers",
                    "One of these numbers belongs to another customer and cannot be added.");
        }

        var emailIsChanging = email?.Value != customer.EmailAddress?.Value;
        if (emailIsChanging)
        {
            var asOf = timeProvider.GetUtcNow();

            // SECURITY gate MEDIUM (second pass) / ADR-028: same as C18 -- refuse BEFORE verifying
            // while locked, so the step-up cannot be used to keep testing passwords through a
            // lockout window.
            if (customer.LoginThrottle.IsLocked(asOf))
                return Violation(BusinessViolationCodes.AccountLocked, "CurrentPassword", "This account is temporarily locked.");

            // SECURITY gate MEDIUM / ADR-028: an account with no password yet must cost the same
            // one Argon2 verify as a genuine wrong-password attempt (see C18's identical comment).
            // An EMPTY current password is not put through Argon2 at all -- Konscious's Argon2id
            // rejects a zero-length password outright, and unlike an unknown identifier or a
            // locked account, "no password was supplied" reveals nothing about this account's
            // state (the caller controls that input directly), so there is no oracle to close here.
            var currentPassword = command.CurrentPassword ?? string.Empty;
            var passwordMatches = !string.IsNullOrEmpty(currentPassword) && (
                customer.PasswordCredential is not null
                    ? passwordHasher.Verify(currentPassword, customer.PasswordCredential)
                    : PasswordHasherDummyVerify(passwordHasher, currentPassword));

            if (!passwordMatches)
            {
                // A step-up failure here IS now treated as a failed login for lockout purposes
                // (unlike before this fix) -- otherwise this endpoint offers an unthrottled
                // password oracle against a session an attacker did not need the password to
                // obtain in the first place.
                customer.RecordFailedLogin(new LoginFailureDto(asOf, command.UserContext, command.CorrelationId));
                await customerRepository.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);
                return Violation(BusinessViolationCodes.ReauthenticationFailed, "CurrentPassword", "The password is incorrect.");
            }
        }

        try
        {
            customer.UpdateContactDetails(command.ToContactDetailsDto(email, additional));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await customerRepository.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(CustomerAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    /// <summary>Burns the same one Argon2 verify a real credential check would, then always
    /// reports a mismatch -- there is no credential to match against (see C18's identical helper).</summary>
    private static bool PasswordHasherDummyVerify(IPasswordHasher passwordHasher, string plaintext)
    {
        passwordHasher.VerifyAgainstDummy(plaintext);
        return false;
    }
}
