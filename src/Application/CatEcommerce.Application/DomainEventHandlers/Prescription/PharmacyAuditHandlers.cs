using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §11.6 -- the <c>BusinessEvent</c> audit rows for the pharmacy moments worth a line of their own. Data changes are audited by HO-ACC-001's
/// change-capture interceptor; these rows say <i>who did what and when</i> at the business level. Every handler writes through
/// <c>SaveAsync(entry, DoNotPublish)</c>, <b>never enqueues</b> (HO-NTF-001 ADR-057) and writes ids, counts and enumerations only -- no allergen text, no
/// instructions, no reason, no name. The <c>EntityType</c> strings are registered in <c>ClinicalAuditEntityTypes.Pharmacy</c> (C-ALL-5).
/// </summary>
internal static class PharmacyAuditEntries
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

/// <summary>The controlled flag has its own audit action: a medicine becoming (or ceasing to be) a controlled drug changes who must witness its handling.</summary>
public sealed class AuditFormularyControlledStatusHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<FormularyMedicineControlledStatusChangedEvent>
{
    public Task HandleAsync(FormularyMedicineControlledStatusChangedEvent @event) => PharmacyAuditEntries.WriteAsync(
        auditRepository, clientIpAccessor, timeProvider, nameof(FormularyMedicineAggregate), @event.MedicineId, "FormularyControlledFlagChanged",
        $"IsControlled={@event.IsControlled}", @event.UserContext!, @event.CorrelationId);
}

/// <summary>
/// Acknowledging a dose or allergy warning is a clinical decision worth a line of its own (US-PHM-02, US-PHM-03). Joined to the prescription's
/// <c>DataChange</c> row by <c>CorrelationId</c>; ids only -- the allergen, the reason and the note live on the prescription's safety record.
/// </summary>
public sealed class AuditPrescriptionAcknowledgementHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<PrescriptionIssuedEvent>
{
    public Task HandleAsync(PrescriptionIssuedEvent @event) => !@event.HadAcknowledgedWarnings
        ? Task.CompletedTask
        : PharmacyAuditEntries.WriteAsync(
            auditRepository, clientIpAccessor, timeProvider, nameof(PrescriptionAggregate), @event.PrescriptionId, "PrescriptionWarningsAcknowledged",
            $"CatId={@event.CatId};VisitId={@event.VisitId};MedicineId={@event.MedicineId}", @event.UserContext!, @event.CorrelationId);
}

/// <summary>A reprint of a label is counted on the dispensing; the print itself is the browser's. Reprints of a controlled-drug label are worth knowing about, so every print
/// is a <c>BusinessEvent</c> row with the running count (ids and numbers only).</summary>
public sealed class AuditLabelPrintedHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<LabelPrintedEvent>
{
    public Task HandleAsync(LabelPrintedEvent @event) => PharmacyAuditEntries.WriteAsync(
        auditRepository, clientIpAccessor, timeProvider, nameof(DispensingAggregate), @event.DispensingId, "LabelPrinted",
        $"PrescriptionId={@event.PrescriptionId};PrintCount={@event.PrintCount};Controlled={@event.IsControlled}", @event.UserContext!, @event.CorrelationId);
}
