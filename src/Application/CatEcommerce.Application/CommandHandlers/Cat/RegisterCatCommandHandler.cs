using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Cat;

/// <summary>
/// SECURITY gate, LOW (2026-09-26): <see cref="RegisterCatCommand.OwnerId"/> is forced to the
/// caller for a customer (<c>CatsController.CurrentCaller</c>), but a staff caller may still name
/// any owner id -- so it must resolve to a real, non-deleted <see cref="CustomerAccountAggregate"/>,
/// the same read-only existence-check-in-the-handler shape
/// <see cref="RecordCatVaccinationCommandHandler"/> already uses for the vet id.
///
/// CODE_REVIEW gate, MEDIUM (2026-09-26, third pass): existence alone is not enough --
/// <c>EfAggregateRootRepository.GetByIdAsync</c> only excludes a hard-deleted
/// (<c>IsMarkedToDelete</c>) row, not a customer whose <see cref="CustomerAccountAggregate.Status"/>
/// has moved to <see cref="CustomerAccountStatus.Merged"/> or
/// <see cref="CustomerAccountStatus.Anonymised"/> -- both are ordinary, present rows. Without this
/// check a staff caller could register a cat to a merged-away or anonymised customer id, orphaning
/// it exactly the way the existence check was meant to prevent. Reuses
/// <see cref="BusinessViolationCodes.AccountNotActive"/> -- the same code
/// <c>MergeCustomerCommandHandler</c> already returns for an identical "this customer record
/// exists but its Status is not Active" check on the surviving customer -- rather than
/// <see cref="BusinessViolationCodes.CustomerNotFound"/> (the record does exist) or a new,
/// duplicate code.
/// </summary>
public sealed class RegisterCatCommandHandler(
    ILogger<RegisterCatCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RegisterCatCommand>
{
    public async Task<CommandResponse> HandleAsync(RegisterCatCommand command)
    {
        logger.LogInformation("Registering cat {CatId}. CorrelationId: {CorrelationId}",
            command.CatId, command.CorrelationId);

        // CustomerAccountAggregate is a read-only lookup -- it is NOT mutated by this command.
        var owner = await customerRepository.GetByIdAsync(command.OwnerId);
        if (owner is null)
            return Violation(BusinessViolationCodes.CustomerNotFound, nameof(command.OwnerId),
                $"Customer '{command.OwnerId}' was not found.");
        if (owner.Status != CustomerAccountStatus.Active)
            return Violation(BusinessViolationCodes.AccountNotActive, nameof(command.OwnerId),
                "This customer record is not active.");

        var cat = new CatAggregate(command.CatId);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            cat.Register(command.ToDomainDto(today));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await catRepository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);

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
