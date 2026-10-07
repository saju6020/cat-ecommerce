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

/// <summary>HO-ACC-001 §11.6/§11.7 / ADR-037. Exactly one row per idle expiry, even under
/// concurrent refused requests -- <c>EndForIdleTimeout</c> is terminal and idempotent-guarded
/// (a second call throws), so only the request that actually transitions the session raises this.</summary>
public sealed class AuditStaffSessionIdleExpiredHandler(
    ILogger<AuditStaffSessionIdleExpiredHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor)
    : IEventHandlerAsync<StaffSessionIdleExpiredEvent>
{
    public async Task HandleAsync(StaffSessionIdleExpiredEvent @event)
    {
        logger.LogInformation("Auditing staff session idle expiry {SessionId}. CorrelationId: {CorrelationId}", @event.SessionId, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "StaffSession", EntityId: @event.SessionId, Action: "StaffSessionIdleExpired",
            Reason: "IdleTimeout", ActorUserId: @event.StaffAccountId, OccurredOn: @event.DetectedOn,
            BeforeValue: $"LastActivityOn={@event.LastActivityOn}",
            AfterValue: $"ExpiredOn={@event.ExpiredOn};IdleTimeoutMinutes={@event.IdleTimeoutMinutes}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId,
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
