using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C15) / US-ACC-10. Duplicate PRIMARY mobile on an active customer -> 400. No
/// hasher, no email.
///
/// SECURITY gate MEDIUM (2026-09-25, HO-ACC-001 §1 Amended -- self-amendment to §11.4 C15):
/// an ADDITIONAL-number match is deliberately NOT a hard refusal here, and never was allowed to be
/// after this fix. Additional phone numbers are never verified (any self-registered customer can
/// put any number, including a victim's real mobile, into their own additionalPhoneNumbers via
/// PUT /api/customers/me -- there is no uniqueness check on that field). Refusing walk-in
/// registration on an additional-number hit let an attacker who had already squatted on a victim's
/// real number as one of their OWN additional numbers permanently block reception from ever
/// registering that victim under their own, genuine primary number -- reception's only recourse
/// (§11.4's own prescribed follow-up, GET /api/staff/customers?mobile=) would open the ATTACKER's
/// record instead, and staff attaching the victim's cats to it, or merging the victim into it,
/// would hand the attacker the victim's clinical history. Unlike squatting on a primary number
/// (recoverable via password reset), the victim has no recovery path at all, because every
/// identifier on the attacker's account is the attacker's own.
///
/// A hit on an additional number is now surfaced only as a "confirm identity" candidate through
/// Q-H2 (<c>FindCustomersQueryHandler</c>), which reports whether a match was against the primary
/// or an additional number so the screen can show the difference -- it is never used to refuse
/// registration.
/// </summary>
public sealed class RegisterWalkInCustomerCommandHandler(
    ILogger<RegisterWalkInCustomerCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository)
    : ICommandHandlerAsync<RegisterWalkInCustomerCommand>
{
    public async Task<CommandResponse> HandleAsync(RegisterWalkInCustomerCommand command)
    {
        logger.LogInformation("Registering walk-in customer {CustomerId}. CorrelationId: {CorrelationId}", command.CustomerId, command.CorrelationId);

        MobileNumber mobile;
        try
        {
            mobile = MobileNumber.Create(command.MobileNumber);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        // SECURITY gate MEDIUM: PRIMARY match only. An additional-number match must never refuse
        // registration -- see this class's summary comment.
        var duplicate = await customerRepository.ExistAsync(c =>
            c.Status == CustomerAccountStatus.Active && c.MobileNumber!.Value == mobile.Value);

        if (duplicate)
            return Violation(BusinessViolationCodes.MobileNumberAlreadyRegistered, "MobileNumber", AccountMessages.MobileInUse);

        var customer = new CustomerAccountAggregate(command.CustomerId);

        try
        {
            customer.RegisterWalkIn(command.ToWalkInRegistrationDto(mobile));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await customerRepository.SaveAsync(customer, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = customer.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(CustomerAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
