using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §11.6 (BR-7, AC-20) -- a controlled-drug entry in the anaesthesia log is also written to the audit log as a <c>BusinessEvent</c> naming the administering vet and the witness
/// (ids only -- the register holds the rest). The event carries no names or quantities (U-X1), so the entry is read back. Runs only for an entry where the drug IS controlled.
/// </summary>
public sealed class AuditControlledDrugEntryHandler(
    IAggregateRootRepository<AnaesthesiaLogEntryAggregate> entryRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<AnaesthesiaEntryLoggedEvent>
{
    public async Task HandleAsync(AnaesthesiaEntryLoggedEvent @event)
    {
        if (!@event.IsControlled) return;

        var entry = await entryRepository.GetByIdAsync(@event.EntryId);
        if (entry is null) return;

        var record = new AuditEntryAggregate(Guid.NewGuid());
        record.Record(new AuditEntryDto(
            EntityType: "AnaesthesiaLogEntryAggregate", EntityId: @event.EntryId, Action: "ControlledDrugAdministered",
            Reason: entry.Kind == AnaesthesiaEntryKind.Correction ? "Controlled drug entry corrected in the anaesthesia log" : "Controlled drug administered in surgery",
            ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null,
            AfterValue: $"SurgeryId={@event.SurgeryId};CatId={@event.CatId};AdministeringVetId={entry.AdministeredById};WitnessId={entry.WitnessId};RegisterEntryId={entry.RegisterEntryId}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(record, EventPublishOption.DoNotPublish);
    }
}
