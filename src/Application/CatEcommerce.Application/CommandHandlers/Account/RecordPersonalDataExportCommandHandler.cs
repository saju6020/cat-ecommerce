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

/// <summary>HO-ACC-001 §11.4 (C17) / US-ACC-13. The controller runs Q-J1 with the same id after this.</summary>
public sealed class RecordPersonalDataExportCommandHandler(
    ILogger<RecordPersonalDataExportCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordPersonalDataExportCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordPersonalDataExportCommand command)
    {
        logger.LogInformation("Recording personal data export. CorrelationId: {CorrelationId}", command.CorrelationId);

        var customer = await customerRepository.GetByIdAsync(command.UserContext.UserId);
        if (customer is null) return Violation(BusinessViolationCodes.CustomerNotFound, "CustomerId", "This account was not found.");

        try
        {
            customer.RecordPersonalDataExport(command.ToExportDto(timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await customerRepository.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = customer.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(CustomerAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
