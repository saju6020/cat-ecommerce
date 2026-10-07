using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C9) / US-ACC-07 / ADR-009. Carries the ADR-009 defence-in-depth guard: the
/// handler re-checks <see cref="ActionRoles"/> against the caller's roles. `RequireActionFilter`
/// is the control (U-F9); this is one line and one test, not a second control.
/// </summary>
public sealed class CreateStaffAccountCommandHandler(
    ILogger<CreateStaffAccountCommandHandler> logger,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository)
    : ICommandHandlerAsync<CreateStaffAccountCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateStaffAccountCommand command)
    {
        logger.LogInformation("Creating staff account {StaffAccountId}. CorrelationId: {CorrelationId}", command.StaffAccountId, command.CorrelationId);

        if (!ActionRoles.TryGetRoles(Actions.StaffAccounts.Create, out var allowedRoles) ||
            !allowedRoles.Intersect(command.UserContext.Roles ?? [], StringComparer.Ordinal).Any())
            return Violation(BusinessViolationCodes.ActionNotPermitted, "Action", "You are not permitted to perform this action.");

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

        if (mobile is null && email is null)
            return Violation(BusinessViolationCodes.IdentifierRequired, "MobileNumber", "At least one of a mobile number or email address is required.");

        if (mobile is not null && await staffRepository.ExistAsync(c => c.MobileNumber!.Value == mobile.Value))
            return Violation(BusinessViolationCodes.MobileNumberAlreadyRegistered, "MobileNumber", "This mobile number is already registered to a staff account.");

        if (email is not null && await staffRepository.ExistAsync(c => c.EmailAddress!.Value == email.Value))
            return Violation(BusinessViolationCodes.EmailAddressAlreadyRegistered, "EmailAddress", "This email address is already registered to a staff account.");

        var staff = new StaffAccountAggregate(command.StaffAccountId);

        try
        {
            staff.Create(command.ToCreationDto(mobile, email));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await staffRepository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = staff.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(StaffAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
