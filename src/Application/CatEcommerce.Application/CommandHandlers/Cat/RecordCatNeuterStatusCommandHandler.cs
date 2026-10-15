using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Cat;

public sealed class RecordCatNeuterStatusCommandHandler(
    ILogger<RecordCatNeuterStatusCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    ICatRecordClock clock)
    : ICommandHandlerAsync<RecordCatNeuterStatusCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCatNeuterStatusCommand command)
    {
        logger.LogInformation("Recording neuter status for cat {CatId}. CorrelationId: {CorrelationId}", command.CatId, command.CorrelationId);
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId),
                $"Cat '{command.CatId}' was not found.");

        var today = await clock.LocalTodayAsync(CancellationToken.None);

        try
        {
            cat.RecordNeuterStatus(command.ToDomainDto(today));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = cat.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(CatAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
