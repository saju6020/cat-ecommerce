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
/// SECURITY gate, LOW (2026-09-26): <see cref="TransferCatOwnershipCommand.NewOwnerId"/> names a
/// customer -- it must resolve to a real, non-deleted <see cref="CustomerAccountAggregate"/>, the
/// same read-only existence-check-in-the-handler shape <see cref="RecordCatVaccinationCommandHandler"/>
/// already uses for the vet id.
///
/// CODE_REVIEW gate, MEDIUM (2026-09-26, third pass): existence alone is not enough -- see
/// <see cref="RegisterCatCommandHandler"/>'s own note on the identical gap. A merged-away or
/// anonymised customer is still an existing row (`EfAggregateRootRepository.GetByIdAsync` only
/// excludes hard-deleted rows), so without this check ownership could be transferred onto a
/// customer id the account side no longer treats as live -- the opposite of what a merge is meant
/// to achieve. Reuses <see cref="BusinessViolationCodes.AccountNotActive"/>, the same code
/// <c>MergeCustomerCommandHandler</c> already returns for an identical check on the surviving
/// customer.
/// </summary>
public sealed class TransferCatOwnershipCommandHandler(
    ILogger<TransferCatOwnershipCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository)
    : ICommandHandlerAsync<TransferCatOwnershipCommand>
{
    public async Task<CommandResponse> HandleAsync(TransferCatOwnershipCommand command)
    {
        logger.LogInformation("Transferring ownership of cat {CatId}. CorrelationId: {CorrelationId}", command.CatId, command.CorrelationId);
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId),
                $"Cat '{command.CatId}' was not found.");

        // CustomerAccountAggregate is a read-only lookup -- it is NOT mutated by this command.
        var newOwner = await customerRepository.GetByIdAsync(command.NewOwnerId);
        if (newOwner is null)
            return Violation(BusinessViolationCodes.CustomerNotFound, nameof(command.NewOwnerId),
                $"Customer '{command.NewOwnerId}' was not found.");
        if (newOwner.Status != CustomerAccountStatus.Active)
            return Violation(BusinessViolationCodes.AccountNotActive, nameof(command.NewOwnerId),
                "This customer record is not active.");

        // OwnerUnchanged is enforced by the aggregate against its own current state -- see
        // CatAggregate.TransferOwnership.
        try
        {
            cat.TransferOwnership(command.ToDomainDto());
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
