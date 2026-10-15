using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Order;

/// <summary>
/// HO-ORD-001 §11.6 (HO-ACC-001 ADR-039, ADR-214) -- <c>CustomerMergedEvent</c> is binding on every context holding a customer id. The duplicate's <b>active
/// cart</b> is merged into the survivor's (or simply re-keyed when the survivor has none -- the unique active-cart index forbids two), and every
/// <b>order</b> moves to the survivor: a customer's history follows them. One <c>UpdateAsync</c> per row; idempotent (a moved row is a no-op, a merged cart
/// is no longer <c>Active</c>). Never enqueues a message (HO-NTF-001 ADR-057).
/// </summary>
public sealed class ReassignOrderingOnCustomerMergedHandler(
    ICartFinder cartFinder,
    IOrderFinder orderFinder,
    IAggregateRootRepository<CartAggregate> carts,
    IAggregateRootRepository<OrderAggregate> orders,
    IReturnFinder returnFinder,
    IAggregateRootRepository<ReturnRequestAggregate> returns,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var cartId in await cartFinder.FindCartIdsByCustomerAsync(@event.DuplicateCustomerId, default))
        {
            var cart = await carts.GetByIdAsync(cartId);
            if (cart is null || cart.Status != CartStatus.Active) continue;

            var survivorCart = await carts.GetByFilterAsync(c => c.CustomerId == @event.SurvivingCustomerId && c.Status == CartStatus.Active);
            if (survivorCart is null)
            {
                if (cart.ReassignCustomer(@event.SurvivingCustomerId)) await carts.UpdateAsync(cart, EventPublishOption.InMemoryPublishOnly);
                continue;
            }

            survivorCart.AbsorbDuplicateCustomerCart(cart, now, settings.GetInt(SettingKeys.OrdersMaxQuantityPerLine), settings.GetInt(SettingKeys.OrdersMaxCartLines));
            await carts.UpdateAsync(survivorCart, EventPublishOption.InMemoryPublishOnly); // commits the duplicate's Merged status too
        }

        foreach (var orderId in await orderFinder.FindOrderIdsByCustomerAsync(@event.DuplicateCustomerId, default))
        {
            var order = await orders.GetByIdAsync(orderId);
            if (order is null || !order.ReassignCustomer(@event.SurvivingCustomerId)) continue;

            await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var returnId in await returnFinder.FindReturnIdsByCustomerAsync(@event.DuplicateCustomerId, default))
        {
            var request = await returns.GetByIdAsync(returnId);
            if (request is null || !request.ReassignCustomer(@event.SurvivingCustomerId)) continue;

            await returns.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>
/// HO-ORD-001 §11.6 (D-11, ADR-214) -- <c>CustomerAccountAnonymisedEvent</c>: a <b>terminal</b> order's recipient, phone and address lines are redacted (the
/// district is kept for reporting); an <b>open</b> order is flagged and the aggregate redacts it at its terminal transition; the active cart is abandoned
/// so nothing is bought later on the account. Nothing is deleted. Idempotent. Never enqueues a message.
/// </summary>
public sealed class RedactOrdersOnCustomerAnonymisedHandler(
    ICartFinder cartFinder,
    IOrderFinder orderFinder,
    IAggregateRootRepository<CartAggregate> carts,
    IAggregateRootRepository<OrderAggregate> orders,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerAccountAnonymisedEvent>
{
    public async Task HandleAsync(CustomerAccountAnonymisedEvent @event)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var cartId in await cartFinder.FindCartIdsByCustomerAsync(@event.CustomerId, default))
        {
            var cart = await carts.GetByIdAsync(cartId);
            if (cart is null || !cart.AbandonForAnonymisation(now)) continue;

            await carts.UpdateAsync(cart, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var orderId in await orderFinder.FindOrderIdsByCustomerAsync(@event.CustomerId, default))
        {
            var order = await orders.GetByIdAsync(orderId);
            if (order is null || !order.FlagCustomerAnonymised()) continue;

            await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
