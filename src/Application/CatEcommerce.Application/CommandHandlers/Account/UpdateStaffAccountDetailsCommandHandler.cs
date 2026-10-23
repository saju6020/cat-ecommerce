using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// Owner, 2026-10-20: an administrator edits a staff member's name, mobile number and email address. The same
/// identifier rules and duplicate checks as <see cref="CreateStaffAccountCommandHandler"/> (excluding this account
/// itself), and the ADR-009 defence-in-depth role re-check. The security stamp is not rotated: open sessions stay,
/// and the next sign-in uses the new identifier.
/// </summary>
public sealed class UpdateStaffAccountDetailsCommandHandler(
    ILogger<UpdateStaffAccountDetailsCommandHandler> logger,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository)
    : ICommandHandlerAsync<UpdateStaffAccountDetailsCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateStaffAccountDetailsCommand command)
    {
        logger.LogInformation("Updating the details of staff account {StaffAccountId}. CorrelationId: {CorrelationId}", command.StaffAccountId, command.CorrelationId);

        if (!ActionRoles.TryGetRoles(Actions.StaffAccounts.UpdateDetails, out var allowedRoles) ||
            !allowedRoles.Intersect(command.UserContext.Roles ?? [], StringComparer.Ordinal).Any())
            return Violation(BusinessViolationCodes.ActionNotPermitted, "Action", "You are not permitted to perform this action.");

        var staff = await staffRepository.GetByIdAsync(command.StaffAccountId);
        if (staff is null)
            return Violation(BusinessViolationCodes.StaffAccountNotFound, "StaffAccountId", "This staff account does not exist.");

        MobileNumber? mobile = null;
        EmailAddress? email = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(command.MobileNumber)) mobile = MobileNumber.Create(command.MobileNumber);
            if (!string.IsNullOrWhiteSpace(command.EmailAddress)) email = EmailAddress.Create(command.EmailAddress);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var staffId = staff.Id;

        if (mobile is not null && await staffRepository.ExistAsync(c => c.Id != staffId && c.MobileNumber!.Value == mobile.Value))
            return Violation(BusinessViolationCodes.MobileNumberAlreadyRegistered, "MobileNumber", "This mobile number is already registered to a staff account.");

        if (email is not null && await staffRepository.ExistAsync(c => c.Id != staffId && c.EmailAddress!.Value == email.Value))
            return Violation(BusinessViolationCodes.EmailAddressAlreadyRegistered, "EmailAddress", "This email address is already registered to a staff account.");

        try
        {
            staff.UpdateDetails(new StaffAccountDetailsUpdateDto(command.FullName.Trim(), mobile, email, command.UserContext, command.CorrelationId));
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
