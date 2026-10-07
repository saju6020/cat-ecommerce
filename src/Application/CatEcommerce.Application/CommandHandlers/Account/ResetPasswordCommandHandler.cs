using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C5) / US-ACC-05 / ADR-034. Every token failure (unknown, expired,
/// superseded, account gone) collapses to one <see cref="BusinessViolationCodes.InvalidPasswordResetToken"/>.
/// A breached or short new password does not consume the token -- the stamp only rotates on success.
/// </summary>
public sealed class ResetPasswordCommandHandler(
    ILogger<ResetPasswordCommandHandler> logger,
    IAggregateRootRepository<PasswordResetTokenAggregate> tokenRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IPasswordResetTokenGenerator tokenGenerator,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ResetPasswordCommand>
{
    public async Task<CommandResponse> HandleAsync(ResetPasswordCommand command)
    {
        logger.LogInformation("Password reset confirm attempt. CorrelationId: {CorrelationId}", command.CorrelationId);

        var tokenHash = tokenGenerator.ComputeHash(command.Token);
        var token = await tokenRepository.GetByFilterAsync(t => t.TokenHash == tokenHash);
        if (token is null) return InvalidToken();

        var asOf = timeProvider.GetUtcNow();

        if (token.AccountKind == AccountKind.Customer)
        {
            var account = await customerRepository.GetByIdAsync(token.AccountId);
            if (account is null) return InvalidToken();

            try
            {
                token.EnsureUsable(asOf, account.SecurityStamp);
            }
            catch (DomainRuleViolationException)
            {
                return InvalidToken();
            }

            if (await breachedPasswordChecker.IsBreachedAsync(command.NewPassword))
                return Violation(BusinessViolationCodes.BreachedPassword, "NewPassword", "This password has appeared in a known data breach. Choose a different one.");

            var credential = passwordHasher.Hash(command.NewPassword);
            account.SetPassword(new PasswordSetDto(credential, command.UserContext, command.CorrelationId));
            await customerRepository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);
        }
        else
        {
            var account = await staffRepository.GetByIdAsync(token.AccountId);
            if (account is null) return InvalidToken();

            try
            {
                token.EnsureUsable(asOf, account.SecurityStamp);
            }
            catch (DomainRuleViolationException)
            {
                return InvalidToken();
            }

            if (await breachedPasswordChecker.IsBreachedAsync(command.NewPassword))
                return Violation(BusinessViolationCodes.BreachedPassword, "NewPassword", "This password has appeared in a known data breach. Choose a different one.");

            var credential = passwordHasher.Hash(command.NewPassword);
            account.SetPassword(new PasswordSetDto(credential, command.UserContext, command.CorrelationId));
            await staffRepository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);
        }

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse InvalidToken() =>
        Violation(BusinessViolationCodes.InvalidPasswordResetToken, "Token", "This reset link is invalid or has expired. Request a new one.");

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(PasswordResetTokenAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
