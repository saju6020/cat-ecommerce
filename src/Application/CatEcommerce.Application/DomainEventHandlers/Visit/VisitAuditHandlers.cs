using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Visit;

/// <summary>
/// HO-EMR-001 §11.6 -- the <c>BusinessEvent</c> audit rows for the clinically significant moments. Data
/// changes are audited by HO-ACC-001's change-capture interceptor; these rows say <i>who did what and when</i>
/// at the business level (AC-20). Every handler writes through <c>SaveAsync(entry, DoNotPublish)</c>, <b>never
/// enqueues</b> (HO-NTF-001 ADR-057) and never mutates a clinical aggregate. Only ids and enums are written:
/// no clinical text, name or file name (U-X1). The <c>EntityType</c> strings are registered in
/// <c>ClinicalAuditEntityTypes.Consultation</c> (C-ALL-5).
/// </summary>
internal static class VisitAuditEntries
{
    public static async Task WriteAsync(
        IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider,
        string entityType, Guid entityId, string action, string? afterValue, UserContext userContext, Guid correlationId)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: entityType, EntityId: entityId, Action: action, Reason: action, ActorUserId: userContext.UserId,
            OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null, AfterValue: afterValue, UserContext: userContext,
            CorrelationId: correlationId, Category: AuditCategory.BusinessEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}

/// <summary>Who finalised the visit, and when -- the attending vet of record (§8.2).</summary>
public sealed class AuditVisitFinalisedHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<VisitFinalisedEvent>
{
    public Task HandleAsync(VisitFinalisedEvent @event) => VisitAuditEntries.WriteAsync(
        auditRepository, clientIpAccessor, timeProvider, "VisitAggregate", @event.VisitId, "VisitFinalised",
        $"FinalisedBy={@event.FinalisedBy};FinalisedOn={@event.FinalisedOn:O}", @event.UserContext, @event.CorrelationId);
}

/// <summary>An addendum is a correction to a frozen record, so who added it is audited as well as stored.</summary>
public sealed class AuditVisitAddendumHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<VisitAddendumAddedEvent>
{
    public Task HandleAsync(VisitAddendumAddedEvent @event) => VisitAuditEntries.WriteAsync(
        auditRepository, clientIpAccessor, timeProvider, "VisitAddendumAggregate", @event.AddendumId, "VisitAddendumAdded",
        $"VisitId={@event.VisitId}", @event.UserContext, @event.CorrelationId);
}

/// <summary>Consent is a legal record (NFR-LEG-4): that it was recorded and that it was withdrawn are both audited, with the kind,
/// the method and the form version -- ids and enumerations only, never the signer's name or the signature.</summary>
public sealed class AuditConsentHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<ConsentRecordedEvent>, IEventHandlerAsync<ConsentWithdrawnEvent>
{
    public Task HandleAsync(ConsentRecordedEvent @event) => VisitAuditEntries.WriteAsync(
        auditRepository, clientIpAccessor, timeProvider, "ConsentRecordAggregate", @event.ConsentId, "ConsentRecorded",
        $"CatId={@event.CatId};AppointmentId={@event.AppointmentId};Kind={@event.Kind.DisplayName};Method={@event.Method.DisplayName};FormVersion={@event.PolicyVersionNumber}",
        @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(ConsentWithdrawnEvent @event) => VisitAuditEntries.WriteAsync(
        auditRepository, clientIpAccessor, timeProvider, "ConsentRecordAggregate", @event.ConsentId, "ConsentWithdrawn",
        $"CatId={@event.CatId};AppointmentId={@event.AppointmentId}", @event.UserContext, @event.CorrelationId);
}

/// <summary>A reversed treatment returned stock to Inventory: who reversed what is a business event of its own (section 11.6). Ids and a flag
/// only -- never the reason, which is free text.</summary>
public sealed class AuditTreatmentReversedHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<TreatmentReversedEvent>
{
    public Task HandleAsync(TreatmentReversedEvent @event) => VisitAuditEntries.WriteAsync(
        auditRepository, clientIpAccessor, timeProvider, "VisitAggregate", @event.VisitId, "TreatmentReversed",
        $"LineId={@event.LineId};StockReturned={@event.StockReturned}", @event.UserContext, @event.CorrelationId);
}
