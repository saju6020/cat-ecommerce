using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Administration;

/// <summary>HO-ADM-001 §11.6 -- a BusinessEvent row with the decider and the decision note (the
/// note lives here, not on the event itself, per §11.5's "no notes, reasons" rule -- read back
/// from the aggregate, since none of the three events carries it).</summary>
public sealed class AuditLeaveDecisionHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IAggregateRootRepository<LeaveRequestAggregate> leaveRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<StaffLeaveApprovedEvent>, IEventHandlerAsync<LeaveRejectedEvent>, IEventHandlerAsync<LeaveCancelledEvent>
{
    public Task HandleAsync(StaffLeaveApprovedEvent @event) => RecordAsync(@event.LeaveRequestId, "LeaveApproved", @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(LeaveRejectedEvent @event) => RecordAsync(@event.LeaveRequestId, "LeaveRejected", @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(LeaveCancelledEvent @event) => RecordAsync(@event.LeaveRequestId, "LeaveCancelled", @event.UserContext, @event.CorrelationId);

    private async Task RecordAsync(Guid leaveRequestId, string action, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        // Gate fix 2026-09-26: this previously recorded AfterValue: null despite the doc comment
        // above claiming "the decision note" was captured -- it never was. DecisionNote lives on
        // the aggregate, not on any of the three events, so it is read back here.
        var leaveRequest = await leaveRepository.GetByIdAsync(leaveRequestId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "LeaveRequest", EntityId: leaveRequestId, Action: action, Reason: action,
            ActorUserId: userContext.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null,
            AfterValue: leaveRequest?.DecisionNote,
            UserContext: userContext, CorrelationId: correlationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
