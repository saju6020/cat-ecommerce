using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// Owner, 2026-10-19: an administrator sets a staff member's password and shares it with them. The same checks as
/// every other password (length by the validator, the breached list here, Argon2id hashing), the ADR-009
/// defence-in-depth role check as in <see cref="CreateStaffAccountCommandHandler"/>, and the existing
/// <c>StaffPasswordSetEvent</c> audit row. Setting it rotates the security stamp, which ends the staff member's open
/// sessions.
/// </summary>
public sealed class SetStaffPasswordCommandHandler(
    ILogger<SetStaffPasswordCommandHandler> logger,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker)
    : ICommandHandlerAsync<SetStaffPasswordCommand>
{
    public async Task<CommandResponse> HandleAsync(SetStaffPasswordCommand command)
    {
        logger.LogInformation("Setting the password of staff account {StaffAccountId}. CorrelationId: {CorrelationId}", command.StaffAccountId, command.CorrelationId);

        if (!ActionRoles.TryGetRoles(Actions.StaffAccounts.SetPassword, out var allowedRoles) ||
            !allowedRoles.Intersect(command.UserContext.Roles ?? [], StringComparer.Ordinal).Any())
            return Violation(BusinessViolationCodes.ActionNotPermitted, "Action", "You are not permitted to perform this action.");

        var staff = await staffRepository.GetByIdAsync(command.StaffAccountId);
        if (staff is null)
            return Violation(BusinessViolationCodes.StaffAccountNotFound, "StaffAccountId", "This staff account does not exist.");

        if (await breachedPasswordChecker.IsBreachedAsync(command.NewPassword))
            return Violation(BusinessViolationCodes.BreachedPassword, "Password", "This password has appeared in a known data breach. Choose a different one.");

        staff.SetPassword(new PasswordSetDto(passwordHasher.Hash(command.NewPassword), command.UserContext, command.CorrelationId,
            SetByAdministratorId: command.UserContext.UserId));
        await staffRepository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(StaffAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
