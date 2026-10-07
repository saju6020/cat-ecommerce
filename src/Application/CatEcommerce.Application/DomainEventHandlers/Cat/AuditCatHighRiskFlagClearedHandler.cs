using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Cat;

/// <summary>
/// A failure here fails the command AFTER the cat has already been committed -- intended,
/// SRS C-9. HO-CAT-001 section 11.7. Category/IP added by HO-ACC-001 §8.11/§11.6/§13.
/// </summary>
public sealed class AuditCatHighRiskFlagClearedHandler(
    ILogger<AuditCatHighRiskFlagClearedHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CatHighRiskFlagClearedEvent>
{
    public async Task HandleAsync(CatHighRiskFlagClearedEvent @event)
    {
        logger.LogInformation("Auditing high-risk flag clear on cat {CatId}. CorrelationId: {CorrelationId}", @event.CatId, @event.CorrelationId);
        var entry = new AuditEntryAggregate(Guid.NewGuid());

        entry.Record(new AuditEntryDto(
            EntityType: "Cat",
            EntityId: @event.CatId,
            Action: "CatHighRiskFlagCleared",
            Reason: @event.Reason,
            ActorUserId: @event.UserContext.UserId,
            OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: "IsHighRisk=true",
            AfterValue: "IsHighRisk=false",
            UserContext: @event.UserContext,
            CorrelationId: @event.CorrelationId,
            Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
