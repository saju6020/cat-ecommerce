using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §8.6 / §11.6 (ADR-160) -- HO-CAT-001's <c>CatWeightRecordedEvent</c> completes the weight item of every open surgery of the cat when the weight falls inside the
/// window (<c>surgery.weightValidityDays</c> before the surgery's local date, or on it). Idempotent (an item already Done with the same reference is skipped), one
/// <c>UpdateAsync</c> per surgery, never enqueues (HO-NTF-001 ADR-057). A failure here fails the request AFTER the weight is committed (domain-events-and-outbox.md §3).
/// </summary>
public sealed class CompleteWeightItemOnWeightRecordedHandler(
    ISurgeryFinder finder,
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<CatWeightRecordAggregate> weightRepository,
    ISurgeryClock clock,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CatWeightRecordedEvent>
{
    public async Task HandleAsync(CatWeightRecordedEvent @event)
    {
        var ids = await finder.FindChecklistOpenByCatAsync(@event.CatId);
        if (ids.Count == 0) return;

        var weight = await weightRepository.GetByIdAsync(@event.WeightRecordId);
        if (weight is null) return;

        foreach (var id in ids)
        {
            var surgery = await surgeryRepository.GetByIdAsync(id);
            if (surgery is null) continue;

            var surgeryDate = await clock.LocalDateOfAsync(surgery.BranchId, surgery.StartsOn, CancellationToken.None);
            var validityDays = settings.GetInt(SettingKeys.SurgeryWeightValidityDays, surgery.BranchId);

            if (surgery.ApplyWeight(weight.Id, weight.RecordedOn, surgeryDate, validityDays, @event.UserContext.UserId, timeProvider.GetUtcNow()))
                await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>Shared by the two consent handlers: the consent records are re-read (never trusted from the event) and the surgery's consent item is made to match them.</summary>
internal static class SurgeryConsentSync
{
    public static async Task SyncAsync(
        Guid appointmentId, Guid actor, ISurgeryFinder finder, IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
        ISurgeryConsentReader consentReader, TimeProvider timeProvider)
    {
        var surgeryId = await finder.FindByAppointmentAsync(appointmentId);
        if (surgeryId is null) return;

        var surgery = await surgeryRepository.GetByIdAsync(surgeryId.Value);
        if (surgery is null) return;

        var coverage = await consentReader.GetCoverageAsync(surgery.CatId, surgery.AppointmentId);
        if (surgery.ApplyConsentCoverage(coverage.Covered, coverage.ConsentId, actor, timeProvider.GetUtcNow()))
            await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
    }
}

/// <summary>HO-SRG-001 §8.6 -- HO-EMR-001's <c>ConsentRecordedEvent</c> (keyed by the procedure-booking appointment, which IS the surgery's appointment). The consent item completes once
/// the records cover every required kind (surgery and anaesthesia). The start gate does not rely on this flag (ADR-161).</summary>
public sealed class CompleteConsentItemOnConsentRecordedHandler(
    ISurgeryFinder finder, IAggregateRootRepository<SurgeryAggregate> surgeryRepository, ISurgeryConsentReader consentReader, TimeProvider timeProvider)
    : IEventHandlerAsync<ConsentRecordedEvent>
{
    public Task HandleAsync(ConsentRecordedEvent @event) =>
        SurgeryConsentSync.SyncAsync(@event.AppointmentId, @event.UserContext.UserId, finder, surgeryRepository, consentReader, timeProvider);
}

/// <summary>HO-SRG-001 §8.6 -- a withdrawn consent reverts the item (unless another valid consent still covers every required kind).</summary>
public sealed class RevertConsentItemOnConsentWithdrawnHandler(
    ISurgeryFinder finder, IAggregateRootRepository<SurgeryAggregate> surgeryRepository, ISurgeryConsentReader consentReader, TimeProvider timeProvider)
    : IEventHandlerAsync<ConsentWithdrawnEvent>
{
    public Task HandleAsync(ConsentWithdrawnEvent @event) =>
        SurgeryConsentSync.SyncAsync(@event.AppointmentId, @event.UserContext.UserId, finder, surgeryRepository, consentReader, timeProvider);
}

/// <summary>
/// HO-SRG-001 §8.7 (US-SRG-03 AC2, BR-5) -- the emergency override is written to the audit log as a <c>BusinessEvent</c> naming the actor, the surgery, the cat and the REASON.
/// The event carries ids only (U-X1), so the reason is read from the aggregate, where it is also stored for good.
/// </summary>
public sealed class AuditConsentOverrideHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<SurgeryConsentOverriddenEvent>
{
    public async Task HandleAsync(SurgeryConsentOverriddenEvent @event)
    {
        var surgery = await surgeryRepository.GetByIdAsync(@event.SurgeryId);
        var reason = surgery?.ConsentOverride?.Reason ?? "Emergency consent override";

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "Surgery", EntityId: @event.SurgeryId, Action: "SurgeryConsentOverridden", Reason: reason, ActorUserId: @event.OverriddenBy,
            OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null, AfterValue: $"CatId={@event.CatId};BranchId={@event.BranchId}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}

/// <summary>HO-SRG-001 §11.6 -- HO-ACC-001's <c>CustomerMergedEvent</c> is binding on every context holding a customer id (ADR-039): the duplicate's surgeries move to the survivor.
/// Idempotent, one <c>UpdateAsync</c> per row, never enqueues.</summary>
public sealed class ReassignSurgeriesOnCustomerMergedHandler(ISurgeryFinder finder, IAggregateRootRepository<SurgeryAggregate> surgeryRepository)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        foreach (var id in await finder.FindByCustomerAsync(@event.DuplicateCustomerId))
        {
            var surgery = await surgeryRepository.GetByIdAsync(id);
            if (surgery is null || !surgery.ReassignOwner(@event.SurvivingCustomerId)) continue;

            await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>HO-SRG-001 §11.6 -- an ownership transfer moves the cat's FUTURE Scheduled surgeries to the new owner (the guards already stop messages to the old owner). Idempotent, never enqueues.</summary>
public sealed class FlagSurgeryOnCatTransferredHandler(ISurgeryFinder finder, IAggregateRootRepository<SurgeryAggregate> surgeryRepository, TimeProvider timeProvider)
    : IEventHandlerAsync<CatOwnershipTransferredEvent>
{
    public async Task HandleAsync(CatOwnershipTransferredEvent @event)
    {
        foreach (var id in await finder.FindFutureScheduledByCatAsync(@event.CatId, timeProvider.GetUtcNow()))
        {
            var surgery = await surgeryRepository.GetByIdAsync(id);
            if (surgery is null || !surgery.ReassignOwner(@event.NewOwnerId)) continue;

            await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
