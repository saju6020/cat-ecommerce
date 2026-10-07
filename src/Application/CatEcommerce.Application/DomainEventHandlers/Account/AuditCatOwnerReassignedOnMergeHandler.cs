using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Account;

/// <summary>HO-ACC-001 §11.6/§11.7/§8.9.</summary>
public sealed class AuditCatOwnerReassignedOnMergeHandler(
    ILogger<AuditCatOwnerReassignedOnMergeHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CatOwnerReassignedOnMergeEvent>
{
    public async Task HandleAsync(CatOwnerReassignedOnMergeEvent @event)
    {
        logger.LogInformation("Auditing cat owner reassignment on merge {CatId}. CorrelationId: {CorrelationId}", @event.CatId, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "Cat", EntityId: @event.CatId, Action: "CatOwnerReassignedOnMerge",
            Reason: "CustomerMerge", ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: $"OwnerId={@event.PreviousOwnerId}", AfterValue: $"OwnerId={@event.NewOwnerId}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId,
            Category: AuditCategory.BusinessEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
