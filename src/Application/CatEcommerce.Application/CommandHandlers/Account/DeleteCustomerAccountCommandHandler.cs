using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C18) / US-ACC-13 / ADR-041. If locked, the response is
/// <see cref="BusinessViolationCodes.AccountLocked"/> BEFORE verifying -- so the step-up is not a
/// way to test passwords during a lockout.
/// </summary>
public sealed class DeleteCustomerAccountCommandHandler(
    ILogger<DeleteCustomerAccountCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<DeleteCustomerAccountCommand>
{
    public async Task<CommandResponse> HandleAsync(DeleteCustomerAccountCommand command)
    {
        logger.LogInformation("Customer account deletion requested. CorrelationId: {CorrelationId}", command.CorrelationId);

        var customer = await customerRepository.GetByIdAsync(command.UserContext.UserId);
        if (customer is null) return Violation(BusinessViolationCodes.CustomerNotFound, "CustomerId", "This account was not found.");

        var asOf = timeProvider.GetUtcNow();

        if (customer.LoginThrottle.IsLocked(asOf))
            return Violation(BusinessViolationCodes.AccountLocked, "CurrentPassword", "This account is temporarily locked.");

        // SECURITY gate MEDIUM / ADR-028: a walk-in with no password yet must cost the same one
        // Argon2 verify as a genuine wrong-password attempt -- otherwise this branch returns
        // faster and discloses "this account has no password" through timing alone.
        var passwordMatches = customer.PasswordCredential is not null
            ? passwordHasher.Verify(command.CurrentPassword, customer.PasswordCredential)
            : PasswordHasherDummyVerify(passwordHasher, command.CurrentPassword);

        if (!passwordMatches)
        {
            customer.RecordFailedLogin(new LoginFailureDto(asOf, command.UserContext, command.CorrelationId));
            await customerRepository.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);
            return Violation(BusinessViolationCodes.ReauthenticationFailed, "CurrentPassword", "The password is incorrect.");
        }

        try
        {
            customer.Anonymise(command.ToAnonymisationDto(asOf));
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
    /// reports a mismatch -- there is no credential to match against.</summary>
    private static bool PasswordHasherDummyVerify(IPasswordHasher passwordHasher, string plaintext)
    {
        passwordHasher.VerifyAgainstDummy(plaintext);
        return false;
    }
}
