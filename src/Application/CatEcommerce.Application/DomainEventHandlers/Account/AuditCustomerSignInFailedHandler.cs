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

/// <summary>HO-ACC-001 §11.6/§11.7. The known-account failure path -- the unknown-identifier
/// path is audited directly by the handler (§11.8), not via an event.</summary>
public sealed class AuditCustomerSignInFailedHandler(
    ILogger<AuditCustomerSignInFailedHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerSignInFailedEvent>
{
    public async Task HandleAsync(CustomerSignInFailedEvent @event)
    {
        logger.LogInformation("Auditing customer sign-in failure {AccountId}. CorrelationId: {CorrelationId}", @event.AccountId, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "CustomerAccount", EntityId: @event.AccountId, Action: "CustomerSignInFailed",
            Reason: @event.Outcome.DisplayName, ActorUserId: @event.AccountId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: $"ConsecutiveFailures={@event.ConsecutiveFailures};LockedUntil={@event.LockedUntil}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId,
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
