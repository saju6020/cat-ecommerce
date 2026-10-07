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
/// Satisfies US-CAT-04 AC2. A failure here fails the command AFTER the cat has already been
/// committed -- .ai/domain-events-and-outbox.md section 3. That is intended: an unaudited
/// flag change is not acceptable (SRS C-9), so it should fail loudly rather than swallow the
/// error. Category/IP added by HO-ACC-001 §8.11/§11.6/§13.
/// </summary>
public sealed class AuditCatHighRiskFlaggedHandler(
    ILogger<AuditCatHighRiskFlaggedHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CatFlaggedHighRiskEvent>
{
    public async Task HandleAsync(CatFlaggedHighRiskEvent @event)
    {
        logger.LogInformation("Auditing high-risk flag on cat {CatId}. CorrelationId: {CorrelationId}", @event.CatId, @event.CorrelationId);
        var entry = new AuditEntryAggregate(Guid.NewGuid());

        entry.Record(new AuditEntryDto(
            EntityType: "Cat",
            EntityId: @event.CatId,
            Action: "CatFlaggedHighRisk",
            Reason: @event.Reason,
            ActorUserId: @event.UserContext.UserId,
            OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: "IsHighRisk=false",
            AfterValue: $"IsHighRisk=true;HandlingNote={@event.HandlingNote}",
            UserContext: @event.UserContext,
            CorrelationId: @event.CorrelationId,
            Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
