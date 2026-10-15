using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>An action that moves an order between Appendix C's states (HO-ORD-001 §8.4). Not persisted.</summary>
public enum OrderTransitionAction
{
    /// <summary>Staff verified a bank transfer: <c>PendingPayment → Confirmed</c>.</summary>
    ConfirmPayment,

    /// <summary>The payment window passed: <c>PendingPayment → Cancelled</c> (<c>PaymentWindowExpired</c>).</summary>
    Expire,

    /// <summary>The customer or staff cancelled: <c>PendingPayment | Confirmed | Packing → Cancelled</c>.</summary>
    Cancel,

    StartPacking,
    Dispatch,
    MarkReadyForCollection,
    RecordDelivery,
    RecordDeliveryFailure,

    /// <summary>Every line was returned and received while <c>Delivered</c>: <c>Delivered → Returned</c>.</summary>
    CompleteReturn,

    /// <summary>The paid amount was fully refunded: <c>Delivered | Returned → Refunded</c>.</summary>
    Refund,

    /// <summary>A counter sale hands the goods over in the same call: <c>Confirmed → Delivered</c>.</summary>
    CollectAtCounter,
}

/// <summary>
/// HO-ORD-001 §8.4 (ADR-197), US-ORD-09 -- the one table of Appendix C's transitions, pure. The aggregate enforces it on every mutation and
/// the read side's <c>allowedActions</c> derive from the same table, so the two cannot drift. <b>No state is added to Appendix C</b>: an
/// expired transfer order is <c>Cancelled</c>, and a pickup order reuses <c>Dispatched</c> (ready for collection) and <c>Delivered</c> (collected).
/// </summary>
public static class OrderTransitionRules
{
    private static readonly IReadOnlyDictionary<(OrderStatus From, OrderTransitionAction Action), OrderStatus> Table =
        new Dictionary<(OrderStatus, OrderTransitionAction), OrderStatus>
        {
            [(OrderStatus.PendingPayment, OrderTransitionAction.ConfirmPayment)] = OrderStatus.Confirmed,
            [(OrderStatus.PendingPayment, OrderTransitionAction.Expire)] = OrderStatus.Cancelled,
            [(OrderStatus.PendingPayment, OrderTransitionAction.Cancel)] = OrderStatus.Cancelled,

            [(OrderStatus.Confirmed, OrderTransitionAction.StartPacking)] = OrderStatus.Packing,
            [(OrderStatus.Confirmed, OrderTransitionAction.Cancel)] = OrderStatus.Cancelled,
            [(OrderStatus.Confirmed, OrderTransitionAction.CollectAtCounter)] = OrderStatus.Delivered,

            [(OrderStatus.Packing, OrderTransitionAction.Dispatch)] = OrderStatus.Dispatched,
            [(OrderStatus.Packing, OrderTransitionAction.MarkReadyForCollection)] = OrderStatus.Dispatched,
            [(OrderStatus.Packing, OrderTransitionAction.Cancel)] = OrderStatus.Cancelled,

            [(OrderStatus.Dispatched, OrderTransitionAction.RecordDelivery)] = OrderStatus.Delivered,
            [(OrderStatus.Dispatched, OrderTransitionAction.RecordDeliveryFailure)] = OrderStatus.Returned,

            [(OrderStatus.Delivered, OrderTransitionAction.CompleteReturn)] = OrderStatus.Returned,
            [(OrderStatus.Delivered, OrderTransitionAction.Refund)] = OrderStatus.Refunded,
            [(OrderStatus.Returned, OrderTransitionAction.Refund)] = OrderStatus.Refunded,
        };

    public static IReadOnlyCollection<(OrderStatus From, OrderTransitionAction Action, OrderStatus To)> AllTransitions =>
        Table.Select(t => (t.Key.From, t.Key.Action, t.Value)).ToList();

    /// <summary>The state a new order starts in: a bank-transfer order waits for money; every other online order is accepted at placement
    /// (COD, pay on collection -- ADR-203); a counter sale starts as <c>PendingPayment</c> and is completed in the same call.</summary>
    public static OrderStatus InitialStatus(OrderPaymentMethod method) =>
        method == OrderPaymentMethod.BankTransfer || method == OrderPaymentMethod.InStore ? OrderStatus.PendingPayment : OrderStatus.Confirmed;

    /// <summary>The state <paramref name="action"/> leads to from <paramref name="from"/>, or <c>null</c> when it is not a transition.</summary>
    public static OrderStatus? Next(OrderStatus from, OrderTransitionAction action) =>
        Table.TryGetValue((from, action), out var to) ? to : null;

    public static bool IsAllowed(OrderStatus from, OrderTransitionAction action) => Table.ContainsKey((from, action));

    /// <summary>The customer may cancel before packing starts (US-ORD-12 AC1); staff may also cancel while packing. Everything else is refused.</summary>
    public static bool CanCancel(OrderStatus from, bool byCustomer) =>
        IsAllowed(from, OrderTransitionAction.Cancel) && !(byCustomer && from == OrderStatus.Packing);

    /// <summary>Returns the target state or throws <c>InvalidOrderTransition</c> naming both states (US-ORD-09 AC2).</summary>
    public static OrderStatus Require(OrderStatus from, OrderTransitionAction action) =>
        Next(from, action) ?? throw new DomainRuleViolationException(
            BusinessViolationCodes.InvalidOrderTransition, OrderMessages.InvalidTransition(StateWords(from), ActionVerb(action)), nameof(OrderStatus));

    /// <summary>The actions open to an order in this state for the staff read model (<c>allowedActions[]</c>). Fulfilment method narrows
    /// dispatch versus ready-for-collection; the caller's own permissions are applied on top by the read side.</summary>
    public static IReadOnlyList<OrderTransitionAction> AllowedActions(OrderStatus from, FulfilmentMethod fulfilment, OrderPaymentMethod paymentMethod) =>
        Table.Keys
            .Where(k => k.From == from)
            .Select(k => k.Action)
            .Where(a => a switch
            {
                OrderTransitionAction.ConfirmPayment or OrderTransitionAction.Expire => paymentMethod == OrderPaymentMethod.BankTransfer,
                OrderTransitionAction.Dispatch => fulfilment == FulfilmentMethod.Delivery,
                OrderTransitionAction.MarkReadyForCollection => fulfilment == FulfilmentMethod.Pickup,
                OrderTransitionAction.CollectAtCounter => fulfilment == FulfilmentMethod.Counter,
                OrderTransitionAction.CompleteReturn => false, // driven by received returns, never chosen
                _ => true,
            })
            .ToList();

    /// <summary>"pending-payment", "confirmed", ... -- the state as a plain adjective for the refusal message.</summary>
    public static string StateWords(OrderStatus status) => status.DisplayName switch
    {
        nameof(OrderStatus.PendingPayment) => "pending-payment",
        var other => other.ToLowerInvariant(),
    };

    /// <summary>The past participle of the refused action: "A delivered order cannot be packed."</summary>
    public static string ActionVerb(OrderTransitionAction action) => action switch
    {
        OrderTransitionAction.ConfirmPayment => "confirmed as paid",
        OrderTransitionAction.Expire => "expired",
        OrderTransitionAction.Cancel => "cancelled",
        OrderTransitionAction.StartPacking => "packed",
        OrderTransitionAction.Dispatch => "dispatched",
        OrderTransitionAction.MarkReadyForCollection => "marked ready for collection",
        OrderTransitionAction.RecordDelivery => "marked delivered",
        OrderTransitionAction.RecordDeliveryFailure => "marked undelivered",
        OrderTransitionAction.CompleteReturn => "marked returned",
        OrderTransitionAction.Refund => "refunded",
        OrderTransitionAction.CollectAtCounter => "collected at the counter",
        _ => action.ToString().ToLowerInvariant(),
    };
}
