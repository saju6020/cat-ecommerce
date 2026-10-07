using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C7). Lazily creates the address book keyed by the customer id (ADR-035) on
/// its first write. A first-write race between two tabs produces a 23505 on the primary key ->
/// 409 via UniqueConstraintExceptionFilter.
/// </summary>
public sealed class AddDeliveryAddressCommandHandler(
    ILogger<AddDeliveryAddressCommandHandler> logger,
    IAggregateRootRepository<CustomerAddressBookAggregate> addressBookRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddDeliveryAddressCommand>
{
    public async Task<CommandResponse> HandleAsync(AddDeliveryAddressCommand command)
    {
        logger.LogInformation("Adding delivery address. CorrelationId: {CorrelationId}", command.CorrelationId);

        var customerId = command.UserContext.UserId;
        var addressBook = await addressBookRepository.GetByIdAsync(customerId);
        var isNew = addressBook is null;

        if (isNew)
        {
            addressBook = new CustomerAddressBookAggregate(customerId);
            addressBook.Open(new CustomerAddressBookOpenDto(command.UserContext, command.CorrelationId));
        }

        try
        {
            addressBook!.AddAddress(command.ToAddDto(timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew)
            await addressBookRepository.SaveAsync(addressBook, EventPublishOption.InMemoryPublishOnly);
        else
            await addressBookRepository.UpdateAsync(addressBook, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = command.AddressId };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(CustomerAddressBookAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
