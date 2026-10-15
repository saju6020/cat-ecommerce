using System.Text.Json;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>What happened to one account when an erasure was taken on its behalf.</summary>
internal enum CustomerErasureStep
{
    Erased,
    AlreadyErased,
    NotFound,
    Merged,
}

/// <summary>
/// The anonymise step shared by C3 (one account, an administrator) and C4 (a list of accounts after a restore). It only calls
/// <see cref="CustomerAccountAggregate.Anonymise"/> and saves the account: the ledger, address book, preferences and notifications follow
/// through the <c>CustomerAccountAnonymisedEvent</c> handlers, exactly as for a self-service deletion (HO-ACC-001 ADR-041).
/// </summary>
internal static class CustomerErasureSteps
{
    public static async Task<CustomerErasureStep> AnonymiseAsync(
        IAggregateRootRepository<CustomerAccountAggregate> customers, Guid customerAccountId, DateTimeOffset now, UserContext actor, Guid correlationId)
    {
        var customer = await customers.GetByIdAsync(customerAccountId);
        if (customer is null) return CustomerErasureStep.NotFound;
        if (customer.Status == CustomerAccountStatus.Anonymised) return CustomerErasureStep.AlreadyErased;
        if (customer.Status == CustomerAccountStatus.Merged) return CustomerErasureStep.Merged;

        customer.Anonymise(new CustomerAnonymisationDto(now, actor, correlationId));
        await customers.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);
        return CustomerErasureStep.Erased;
    }

    public static Task WriteBusinessEventAsync(
        IAggregateRootRepository<AuditEntryAggregate> audit, IClientIpAccessor clientIp, DateTimeOffset now, UserContext actor, Guid correlationId,
        string entityType, Guid entityId, string action, string reason, string? afterValue) =>
        OperationsAudit.WriteBusinessEventAsync(audit, clientIp, now, actor, correlationId, entityType, entityId, action, reason, afterValue);
}

/// <summary>
/// HO-XCT-001 section 11.4 C3 / FR-ACC-11. An administrator takes a deletion request on behalf of a customer. Not found, merged (OQ-6: erase the
/// surviving record) and the active/anonymised cases are answered here; an already-anonymised account is a successful no-op
/// (<c>isReapplication</c>). The account change is followed by ONE business-event audit row naming the channel -- written directly, as
/// HO-ACC-001 C11 does -- and the anonymisation event handlers open the ledger and clear the other contexts.
/// </summary>
public sealed class EraseCustomerOnRequestCommandHandler(
    IAggregateRootRepository<CustomerAccountAggregate> customers,
    IAggregateRootRepository<AuditEntryAggregate> audit,
    IClientIpAccessor clientIp,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<EraseCustomerOnRequestCommand>
{
    public async Task<CommandResponse> HandleAsync(EraseCustomerOnRequestCommand command)
    {
        var now = timeProvider.GetUtcNow();
        CustomerErasureStep step;
        try
        {
            step = await CustomerErasureSteps.AnonymiseAsync(customers, command.CustomerAccountId, now, command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(CustomerAccountAggregate));
        }

        switch (step)
        {
            case CustomerErasureStep.NotFound:
                return OperationsResponses.Violation(BusinessViolationCodes.CustomerAccountNotFound, nameof(command.CustomerAccountId), OperationsMessages.CustomerAccountNotFound, nameof(CustomerAccountAggregate));
            case CustomerErasureStep.Merged:
                return OperationsResponses.Violation(BusinessViolationCodes.CustomerRecordMerged, nameof(command.CustomerAccountId), OperationsMessages.CustomerRecordMerged, nameof(CustomerAccountAggregate));
            case CustomerErasureStep.AlreadyErased:
                return OperationsResponses.Ok(new ErasureOnRequestResult(IsReapplication: true));
        }

        // The audit row is immutable: write the CANONICAL code names (the validator parses case-insensitively, so a request may say "inpersonphotoid").
        EnumerationParsing.TryParse<ErasureRequestChannel>(command.Channel, out var parsedChannel);
        EnumerationParsing.TryParse<ErasureIdentityCheck>(command.IdentityCheck, out var parsedIdentityCheck);
        var channel = parsedChannel!.DisplayName;
        var identityCheck = parsedIdentityCheck!.DisplayName;

        await CustomerErasureSteps.WriteBusinessEventAsync(
            audit, clientIp, now, command.UserContext, command.CorrelationId,
            entityType: "CustomerAccount", entityId: command.CustomerAccountId, action: "CustomerErasureRequestedByStaff",
            reason: $"Staff-taken erasure by {channel}; identity check {identityCheck}",
            afterValue: JsonSerializer.Serialize(new { channel = channel, identityCheck = identityCheck }));

        return OperationsResponses.Ok(new ErasureOnRequestResult(IsReapplication: false));
    }
}
