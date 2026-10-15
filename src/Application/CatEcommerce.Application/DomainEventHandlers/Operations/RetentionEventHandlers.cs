using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.6 / ADR-303. One business-event row for a finished run: its id, trigger, status and pass counts. The run itself is excluded from
/// change capture, so this single row replaces the roughly seven <c>DataChange</c> rows a night it would otherwise write.
/// </summary>
public sealed class AuditRetentionRunFinishedHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<RetentionRunFinishedEvent>
{
    public async Task HandleAsync(RetentionRunFinishedEvent @event)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "RetentionRun", EntityId: @event.RetentionRunId, Action: "RetentionRunFinished",
            Reason: @event.RunStatus.DisplayName, ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null,
            AfterValue: JsonSerializer.Serialize(new
            {
                trigger = @event.Trigger.DisplayName, status = @event.RunStatus.DisplayName, passes = @event.PassCount,
                affected = @event.TotalAffected, held = @event.TotalHeld, failed = @event.TotalFailed,
            }),
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
