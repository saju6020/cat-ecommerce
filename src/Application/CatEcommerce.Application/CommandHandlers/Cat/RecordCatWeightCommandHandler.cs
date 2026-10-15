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

/// <summary>
/// Loads CatAggregate only to confirm it exists. RecordCatWeight creates and saves a
/// CatWeightRecordAggregate -- the cat itself is not mutated and not saved. Weight history is
/// its own aggregate; HO-CAT-001 section 8.1 / ADR-023.
/// </summary>
public sealed class RecordCatWeightCommandHandler(
    ILogger<RecordCatWeightCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<CatWeightRecordAggregate> weightRepository,
    ICatRecordClock clock)
    : ICommandHandlerAsync<RecordCatWeightCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCatWeightCommand command)
    {
        logger.LogInformation("Recording weight for cat {CatId}. CorrelationId: {CorrelationId}", command.CatId, command.CorrelationId);
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId),
                $"Cat '{command.CatId}' was not found.");

        var weightRecord = new CatWeightRecordAggregate(command.WeightRecordId);
        var today = await clock.LocalTodayAsync(CancellationToken.None);

        try
        {
            weightRecord.Record(command.ToDomainDto(today));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await weightRepository.SaveAsync(weightRecord, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = weightRecord.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(CatWeightRecordAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
