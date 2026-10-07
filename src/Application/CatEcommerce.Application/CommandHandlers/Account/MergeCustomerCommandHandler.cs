using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>HO-ACC-001 §11.4 (C16) / US-ACC-11 / ADR-039. Cats move in
/// <c>ReassignCatsOnCustomerMergedHandler</c>, off <see cref="Domain.Events.Account.CustomerMergedEvent"/>.</summary>
public sealed class MergeCustomerCommandHandler(
    ILogger<MergeCustomerCommandHandler> logger,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<MergeCustomerCommand>
{
    public async Task<CommandResponse> HandleAsync(MergeCustomerCommand command)
    {
        logger.LogInformation("Merging customer {DuplicateCustomerId} into {SurvivingCustomerId}. CorrelationId: {CorrelationId}",
            command.DuplicateCustomerId, command.SurvivingCustomerId, command.CorrelationId);

        var duplicate = await customerRepository.GetByIdAsync(command.DuplicateCustomerId);
        if (duplicate is null) return Violation(BusinessViolationCodes.CustomerNotFound, "DuplicateCustomerId", "This customer record was not found.");

        var survivor = await customerRepository.GetByIdAsync(command.SurvivingCustomerId);
        if (survivor is null) return Violation(BusinessViolationCodes.CustomerNotFound, "SurvivingCustomerId", "This customer record was not found.");

        if (survivor.Status != CustomerAccountStatus.Active)
            return Violation(BusinessViolationCodes.AccountNotActive, "SurvivingCustomerId", "The surviving customer record is not active.");

        try
        {
            // CODE_REVIEW gate, MEDIUM: MergedOn is threaded in from here, the same way
            // Anonymise's AnonymisedOn already is -- MergeInto must not read the wall clock itself.
            var asOf = timeProvider.GetUtcNow();
            duplicate.MergeInto(command.ToMergeDto(survivor.MobileNumber, survivor.AdditionalPhoneNumbers, survivor.EmailAddress, asOf));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        // Read the aggregate's own raised event BEFORE UpdateAsync -- the repository clears
        // Events after dispatching them, at the end of the same call.
        var isReapplication = duplicate.Events.OfType<CustomerMergedEvent>().FirstOrDefault()?.IsReapplication ?? false;

        await customerRepository.UpdateAsync(duplicate, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse
        {
            IsSuccess = true,
            Result = new { survivingCustomerId = survivor.Id, isReapplication }
        };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(CustomerAccountAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
