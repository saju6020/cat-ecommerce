using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>HO-ACC-001 §11.4 (C1) / US-ACC-01.</summary>
public sealed class RegisterCustomerCommandHandler(
    ILogger<RegisterCustomerCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker)
    : ICommandHandlerAsync<RegisterCustomerCommand>
{
    public async Task<CommandResponse> HandleAsync(RegisterCustomerCommand command)
    {
        logger.LogInformation("Registering customer {CustomerId}. CorrelationId: {CorrelationId}", command.CustomerId, command.CorrelationId);

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

        // Step 2: uniqueness pre-checks, one per supplied identifier, mobile first. Return on the
        // first hit. Neither message mentions the other identifier (A5/A6, §20.6).
        if (mobile is not null && await customerRepository.ExistAsync(c => c.MobileNumber!.Value == mobile.Value))
            return Violation(BusinessViolationCodes.MobileNumberAlreadyRegistered, "MobileNumber", AccountMessages.MobileInUse);

        if (email is not null && await customerRepository.ExistAsync(c => c.EmailAddress!.Value == email.Value))
            return Violation(BusinessViolationCodes.EmailAddressAlreadyRegistered, "EmailAddress", AccountMessages.EmailInUse);

        // Step 3: breached check, before hashing and before any write.
        if (await breachedPasswordChecker.IsBreachedAsync(command.Password))
            return Violation(BusinessViolationCodes.BreachedPassword, "Password", "This password has appeared in a known data breach. Choose a different one.");

        var credential = passwordHasher.Hash(command.Password);

        var customer = new CustomerAccountAggregate(command.CustomerId);

        try
        {
            customer.Register(command.ToRegistrationDto(mobile, email, credential));
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
