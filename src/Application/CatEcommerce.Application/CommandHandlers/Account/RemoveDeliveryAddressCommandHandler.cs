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

public sealed class RemoveDeliveryAddressCommandHandler(
    ILogger<RemoveDeliveryAddressCommandHandler> logger,
    IAggregateRootRepository<CustomerAddressBookAggregate> addressBookRepository)
    : ICommandHandlerAsync<RemoveDeliveryAddressCommand>
{
    public async Task<CommandResponse> HandleAsync(RemoveDeliveryAddressCommand command)
    {
        logger.LogInformation("Removing delivery address. CorrelationId: {CorrelationId}", command.CorrelationId);

        var addressBook = await addressBookRepository.GetByIdAsync(command.UserContext.UserId);
        if (addressBook is null)
            return Violation(BusinessViolationCodes.DeliveryAddressNotFound, "AddressId", "This delivery address was not found.");

        try
        {
            addressBook.RemoveAddress(command.ToRemoveDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await addressBookRepository.UpdateAsync(addressBook, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(CustomerAddressBookAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
