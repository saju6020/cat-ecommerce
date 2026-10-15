using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Order;

/// <summary>
/// HO-ORD-001 §11.6 (BR-17, C-9) -- the <b>who</b> of money. Order events carry ids only (U-A8), so this handler writes a <c>BusinessEvent</c> audit row naming
/// the actor for each money-relevant event: a confirmation (the order is accepted or paid), a transfer held for a second person, a delivery that collected
/// cash, a FEFO reallocation and a counter sale. The data change itself is audited by HO-ACC-001's interceptor in the same transaction; this adds the actor
/// and the meaning. One row per event; never enqueues a message (HO-NTF-001 ADR-057).
/// </summary>
public sealed class AuditOrderMoneyEventsHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<OrderConfirmedEvent>, IEventHandlerAsync<OrderPaymentHeldForApprovalEvent>, IEventHandlerAsync<OrderDeliveredEvent>,
      IEventHandlerAsync<OrderLineReallocatedEvent>, IEventHandlerAsync<CounterSaleCompletedEvent>, IEventHandlerAsync<RefundIssuedEvent>
{
    public Task HandleAsync(OrderConfirmedEvent @event) =>
        RecordAsync(@event.OrderId, "OrderConfirmed", @event.InvoiceId is { } invoice ? $"InvoiceId={invoice}" : null, @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(OrderPaymentHeldForApprovalEvent @event) =>
        RecordAsync(@event.OrderId, "OrderPaymentHeldForApproval", $"Amount={@event.Amount.Amount:0.00} {@event.Amount.CurrencyCode}", @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(OrderDeliveredEvent @event) =>
        @event.HadCodCollection ? RecordAsync(@event.OrderId, "OrderDeliveredWithCodCollection", null, @event.UserContext, @event.CorrelationId) : Task.CompletedTask;

    public Task HandleAsync(OrderLineReallocatedEvent @event) =>
        RecordAsync(@event.OrderId, "OrderLineReallocated", $"LineId={@event.LineId}; From={@event.FromBatchId}; To={@event.ToBatchId}", @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(CounterSaleCompletedEvent @event) =>
        RecordAsync(@event.OrderId, "CounterSaleCompleted", $"Total={@event.GrandTotal.Amount:0.00} {@event.GrandTotal.CurrencyCode}", @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(RefundIssuedEvent @event) =>
        RecordAsync(@event.OrderId, "RefundIssued", $"RefundId={@event.RefundId}; Method={@event.Method}; Amount={@event.Amount.Amount:0.00} {@event.Amount.CurrencyCode}", @event.UserContext, @event.CorrelationId);

    private async Task RecordAsync(Guid orderId, string action, string? after, UserContext? userContext, Guid correlationId)
    {
        if (userContext is null) return;

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "OrderAggregate", EntityId: orderId, Action: action, Reason: action, ActorUserId: userContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: after, UserContext: userContext, CorrelationId: correlationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
