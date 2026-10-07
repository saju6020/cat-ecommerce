using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>HO-ACC-001 §11.4 (C10) / US-ACC-07 / ADR-009 / OQ-11.</summary>
public sealed class ChangeStaffRolesCommandHandler(
    ILogger<ChangeStaffRolesCommandHandler> logger,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository)
    : ICommandHandlerAsync<ChangeStaffRolesCommand>
{
    public async Task<CommandResponse> HandleAsync(ChangeStaffRolesCommand command)
    {
        logger.LogInformation("Changing staff roles for {StaffAccountId}. CorrelationId: {CorrelationId}", command.StaffAccountId, command.CorrelationId);

        if (!ActionRoles.TryGetRoles(Actions.StaffAccounts.ChangeRoles, out var allowedRoles) ||
            !allowedRoles.Intersect(command.UserContext.Roles ?? [], StringComparer.Ordinal).Any())
            return Violation(BusinessViolationCodes.ActionNotPermitted, "Action", "You are not permitted to perform this action.");

        var staff = await staffRepository.GetByIdAsync(command.StaffAccountId);
        if (staff is null) return Violation(BusinessViolationCodes.StaffAccountNotFound, "StaffAccountId", "This staff account was not found.");

        var losesAdministrator = staff.RoleIds.Contains(StaffRoles.Administrator, StringComparer.Ordinal) &&
                                  !command.RoleIds.Contains(StaffRoles.Administrator, StringComparer.Ordinal);

        if (losesAdministrator)
        {
            // A repository check in the handler, not domain logic (ADR-015): is there an
            // Administrator other than this account? OQ-11.
            var anotherAdministratorExists = await staffRepository.ExistAsync(
                s => s.Id != staff.Id && s.RoleIds.Contains(StaffRoles.Administrator));

            if (!anotherAdministratorExists)
                return Violation(BusinessViolationCodes.LastAdministratorCannotBeRemoved, "RoleIds",
                    "This is the last account with the Administrator role and cannot lose it.");
        }

        try
        {
            staff.ChangeRoles(command.ToRoleChangeDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

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
