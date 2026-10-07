using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Product;

/// <summary>HO-SHP-001 section 11.6 -- changing who may buy a therapeutic food is a
/// safety-relevant act. Never enqueues (HO-NTF-001 ADR-057); a failure here fails the request
/// after the source change committed -- re-sending the source command is the recovery
/// (.ai/domain-events-and-outbox.md section 3).</summary>
public sealed class AuditPrescriptionFlagHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<ProductPrescriptionRequirementChangedEvent>
{
    public async Task HandleAsync(ProductPrescriptionRequirementChangedEvent @event)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "Product", EntityId: @event.ProductId, Action: "PrescriptionRequirementChanged",
            Reason: "PrescriptionRequirementChanged", ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: @event.PrescriptionRequired.ToString(),
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
