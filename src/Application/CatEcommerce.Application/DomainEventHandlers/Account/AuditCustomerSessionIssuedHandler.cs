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

/// <summary>HO-ACC-001 §11.6/§11.7.</summary>
public sealed class AuditCustomerSessionIssuedHandler(
    ILogger<AuditCustomerSessionIssuedHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerSessionIssuedEvent>
{
    public async Task HandleAsync(CustomerSessionIssuedEvent @event)
    {
        logger.LogInformation("Auditing customer session issued {SessionId}. CorrelationId: {CorrelationId}", @event.SessionId, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "CustomerSession", EntityId: @event.SessionId, Action: "CustomerSessionIssued",
            Reason: "SignIn", ActorUserId: @event.CustomerAccountId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: $"ExpiresOn={@event.ExpiresOn}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId,
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
