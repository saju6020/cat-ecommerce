using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Clinic;

/// <summary>HO-APT-001 §11.6 -- HO-ACC-001 <c>CustomerMergedEvent</c> is binding on every context
/// holding a customer id (ADR-039). Every appointment and waitlist entry of the duplicate moves to
/// the survivor; idempotent (re-running on an already-reassigned row is a no-op via each
/// aggregate's own <c>ReassignOwner</c>). No-show counts follow because they are derived (§8.8).</summary>
public sealed class ReassignAppointmentsOnCustomerMergedHandler(
    IFlaggableAppointmentFinder appointmentFinder,
    IWaitlistEntryFinder waitlistEntryFinder,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<WaitlistEntryAggregate> waitlistRepository)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        foreach (var id in await appointmentFinder.FindByCustomerAsync(@event.DuplicateCustomerId))
        {
            var appointment = await appointmentRepository.GetByIdAsync(id);
            if (appointment is null) continue;

            appointment.ReassignOwner(new AppointmentOwnerReassignDto(@event.SurvivingCustomerId, @event.UserContext, @event.CorrelationId));
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var id in await waitlistEntryFinder.FindByCustomerAsync(@event.DuplicateCustomerId))
        {
            var entry = await waitlistRepository.GetByIdAsync(id);
            if (entry is null) continue;

            entry.ReassignOwner(@event.SurvivingCustomerId);
            await waitlistRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>HO-APT-001 §11.6 -- a no-show is held against a customer, so who recorded it matters.</summary>
public sealed class AuditNoShowHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<AppointmentNoShowRecordedEvent>
{
    public async Task HandleAsync(AppointmentNoShowRecordedEvent @event)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "AppointmentAggregate", EntityId: @event.AppointmentId, Action: "AppointmentNoShowRecorded",
            Reason: "AppointmentNoShowRecorded", ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: $"CustomerId={@event.CustomerId}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
