using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Boarding;

/// <summary>
/// HO-BRD-001 section 8.6 (US-BRD-04 AC3). A vaccination override is a BusinessEvent audit row naming the stay, the actor and the REASON -- read back from the
/// stay cat, because an event carries no free text (U-X1). Mutates only the audit log; never enqueues a message (HO-NTF-001 ADR-057).
/// </summary>
public sealed class AuditVaccinationOverrideHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<VaccinationGateOverriddenEvent>
{
    public async Task HandleAsync(VaccinationGateOverriddenEvent @event)
    {
        var stay = await stayRepository.GetByIdAsync(@event.StayId);
        var cat = stay?.Cats.FirstOrDefault(c => c.CatId == @event.CatId);
        if (cat is null) return;

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "BoardingStayAggregate", EntityId: @event.StayId, Action: "BoardingVaccinationOverridden",
            Reason: cat.GateOverrideReason ?? "", ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: $"CatId={@event.CatId}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}

/// <summary>
/// SECURITY (2026-10-15). Staff changing a reserved stay vet authorisation or spending limit write a BusinessEvent audit row with the actor, the reason (read back from the stay cat) and the
/// before and after values. Mutates only the audit log; never enqueues.
/// </summary>
public sealed class AuditVetAuthorityChangeHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<StayVetAuthorityChangedEvent>
{
    public async Task HandleAsync(StayVetAuthorityChangedEvent @event)
    {
        var stay = await stayRepository.GetByIdAsync(@event.StayId);
        var cat = stay?.Cats.FirstOrDefault(c => c.CatId == @event.CatId);
        if (cat is null) return;

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "BoardingStayAggregate", EntityId: @event.StayId, Action: "BoardingVetAuthorityChanged",
            Reason: cat.VetAuthorityChangeReason ?? "", ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: $"CatId={@event.CatId}; Authorised={@event.BeforeAuthorised}; Limit={@event.BeforeLimit:0.00}",
            AfterValue: $"CatId={@event.CatId}; Authorised={@event.AfterAuthorised}; Limit={@event.AfterLimit:0.00}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
