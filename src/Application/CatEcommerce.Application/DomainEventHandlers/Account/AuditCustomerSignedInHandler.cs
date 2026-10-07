using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.6/§11.7. A failure here fails the command AFTER the sign-in already committed
/// (`.ai/domain-events-and-outbox.md` §3) -- intended: an unaudited authentication event is not
/// acceptable (C-9).
/// </summary>
public sealed class AuditCustomerSignedInHandler(
    ILogger<AuditCustomerSignedInHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor)
    : IEventHandlerAsync<CustomerSignedInEvent>
{
    public async Task HandleAsync(CustomerSignedInEvent @event)
    {
        logger.LogInformation("Auditing customer sign-in {AccountId}. CorrelationId: {CorrelationId}", @event.AccountId, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "CustomerAccount", EntityId: @event.AccountId, Action: "CustomerSignedIn",
            Reason: "SignInSucceeded", ActorUserId: @event.AccountId, OccurredOn: @event.OccurredOn,
            BeforeValue: null, AfterValue: null, UserContext: @event.UserContext, CorrelationId: @event.CorrelationId,
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
