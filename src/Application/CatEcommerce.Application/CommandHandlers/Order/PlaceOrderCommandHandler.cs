using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §8.7, §11.4 C9 (US-ORD-02 to US-ORD-08, US-ORD-18 AC1, US-SHP-10 AC3) -- place the signed-in customer's order. The checks run in the order
/// §8.7 gives: a signed-in customer (no guest checkout, OQ-1); no pending policy (<c>PolicyAcceptanceRequired</c>); the recomputed quote's fingerprint equals
/// the customer's (<c>CheckoutQuoteChanged</c>, ADR-200 -- a price, a rate, a stock level or an authorisation that moved since the quote changes it); the
/// quote can be placed; a delivery has a phone to call. Then, in <b>one database transaction</b> (ADR-215):
/// <list type="number">
/// <item>the order is created from the quote's own numbers and its stock reserved FEFO by Inventory, the batches recorded on the lines (ADR-208);</item>
/// <item>the discount use is counted on the <b>tracked</b> code (a concurrent last use collides on <c>xmin</c> -> the client re-quotes) and the placed
/// lines leave the <b>tracked</b> cart;</item>
/// <item>cash on delivery and pay on collection are accepted at once: the order's charges are recorded through Billing's enlisting recorder and
/// the confirmation message enqueued; a bank-transfer order instead waits <c>PendingPayment</c> with the bank details enqueued (ADR-203);</item>
/// <item><c>SaveAsync(order)</c> commits all of it together; then, for the accepted orders, Billing issues the invoice from those charges (its published
/// command) and the order links it. Any failure rolls the whole transaction back.</item>
/// </list>
/// A business refusal is <b>returned</b> (ADR-014); the stock race at commit surfaces as <c>StockNoLongerAvailable</c> (<c>409</c>).
/// </summary>
public sealed class PlaceOrderCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<CartAggregate> carts,
    IAggregateRootRepository<DiscountCodeAggregate> discountCodes,
    IAggregateRootRepository<BranchAggregate> branches,
    IAggregateRootRepository<BankTransferAccountAggregate> bankAccounts,
    ICheckoutInputsLoader inputsLoader,
    ICustomerDirectory customers,
    IOrderNumberGenerator numbers,
    IStockLedger stock,
    IBillableChargeRecorder charges,
    IOrderBillingReader billingReader,
    ITransactionalScope transaction,
    IInMemoryDispatcher dispatcher,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PlaceOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(PlaceOrderCommand command)
    {
        if (CartCaller.CustomerId(command) is not { } customerId)
            return Violation(BusinessViolationCodes.CartNotFound, "UserContext", "Sign in to place an order.");

        var now = timeProvider.GetUtcNow();
        var customer = await customers.GetCustomerAsync(customerId, default);
        if (customer is not { IsActive: true })
            return Violation(BusinessViolationCodes.CartNotFound, "UserContext", "Sign in to place an order.");

        var choice = new FulfilmentChoice(
            command.FulfilmentMethod, command.DeliveryAddressId, command.ServiceLevel, command.SlotDate, command.SlotWindowStart, command.PickupBranchId,
            command.PaymentMethod, string.IsNullOrWhiteSpace(command.DiscountCode) ? null : command.DiscountCode.Trim());

        var inputs = await inputsLoader.LoadAsync(customerId, choice, now, default);
        var quote = CheckoutPricing.Quote(inputs);

        if (quote.PendingPolicies.Count > 0)
            return Violation(BusinessViolationCodes.PolicyAcceptanceRequired, "Policies", OrderMessages.PolicyAcceptanceRequired);

        if (!string.Equals(quote.Fingerprint, command.QuoteFingerprint, StringComparison.Ordinal))
            return Violation(BusinessViolationCodes.CheckoutQuoteChanged, nameof(command.QuoteFingerprint), OrderMessages.CheckoutQuoteChanged);

        if (!quote.CanPlace) return OrderPlacement.RefusalFor(quote);

        var recipientPhone = string.IsNullOrWhiteSpace(command.RecipientPhone) ? customer.Mobile : command.RecipientPhone.Trim();
        if (command.FulfilmentMethod == FulfilmentMethod.Delivery && string.IsNullOrWhiteSpace(recipientPhone))
            return Violation(BusinessViolationCodes.RecipientPhoneRequired, nameof(command.RecipientPhone), OrderMessages.RecipientPhoneRequired);

        var recipientName = string.IsNullOrWhiteSpace(command.RecipientName) ? customer.FullName : command.RecipientName.Trim();
        var branch = await branches.GetByIdAsync(quote.FulfilmentBranchId);
        if (branch is null) return Violation(BusinessViolationCodes.PickupBranchUnavailable, nameof(command.PickupBranchId), "That branch is not available.");

        try
        {
            return await transaction.RunAsync(
                () => PlaceAsync(command, customerId, now, inputs, quote, recipientName, recipientPhone, branch),
                response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            // A refusal inside the unit of work (the aggregate's own, Redeem at the limit, or the stock race at commit) rolled it back as a whole.
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
    }

    private async Task<CommandResponse> PlaceAsync(
        PlaceOrderCommand command, Guid customerId, DateTimeOffset now, CheckoutInputs inputs, CheckoutQuote quote, string recipientName, string? recipientPhone, BranchAggregate branch)
    {
        var appliedDiscount = quote.Discount is { Applied: true } && inputs.Discount is not null ? inputs.Discount : null;

        var order = new OrderAggregate(command.OrderId);
        order.Place(new PlaceOrderDto(
            await numbers.NextAsync(now), OrderChannel.Online, customerId, quote.FulfilmentBranchId, command.FulfilmentMethod, command.PaymentMethod, quote.CurrencyCode,
            OrderPlacement.FulfilmentFor(inputs, quote, recipientName, recipientPhone), quote.Lines.ToDomainDtos(), quote.Totals.ToOrderTotals(),
            appliedDiscount?.DiscountCodeId, appliedDiscount?.Code, quote.Fingerprint, command.IdempotencyKey, now, command.UserContext!, command.CorrelationId,
            quote.DeliveryTaxOf()));

        // ── stock: Inventory chooses the batches (FEFO); Ordering records them ──
        var reserved = await OrderStock.ReserveAsync(stock, transaction, order);
        if (!reserved.IsSuccess) return Violation(reserved.ViolationCode!, reserved.PropertyName!, reserved.Message!);
        order.RecordAllocations(reserved.Allocations);

        // ── tracked roots of this unit of work: the discount code and the cart ──
        if (appliedDiscount is not null)
        {
            var code = await discountCodes.GetByIdAsync(appliedDiscount.DiscountCodeId);
            if (code is null) return Violation(BusinessViolationCodes.CheckoutQuoteChanged, nameof(command.DiscountCode), OrderMessages.CheckoutQuoteChanged);
            code.Redeem();
        }

        var cart = await carts.GetByFilterAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active);
        cart?.RemovePlacedLines(quote.Lines.Select(l => l.LineRef), now);

        // ── how the order is paid ──
        BankTransferAccountAggregate? account = null;
        if (command.PaymentMethod == OrderPaymentMethod.BankTransfer)
        {
            account = await bankAccounts.GetByIdAsync(BankTransferAccountAggregate.SingletonId);
            if (account is not { IsActive: true })
                return Violation(BusinessViolationCodes.PaymentMethodUnavailable, nameof(command.PaymentMethod), OrderMessages.PaymentMethodUnavailable);

            order.AwaitTransfer(now.AddHours(settings.GetInt(SettingKeys.OrdersBankTransferWindowHours)));
            OrderNotifications.EnqueueAwaitingTransfer(outbox, links, order, account, branch.TimeZoneId, command.CorrelationId, command.UserContext!);
        }
        else
        {
            var serviceDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId)).DateTime);
            var deliveryTaxClass = order.Totals.DeliveryCharge > 0m ? (await billingReader.GetDeliveryTaxAsync(now, default)).TaxClassId : null;
            await OrderBilling.RecordChargesAsync(charges, OrderBilling.BuildCharges(order, deliveryTaxClass, serviceDate));
            OrderNotifications.EnqueueConfirmed(outbox, links, order, paid: false, command.CorrelationId, command.UserContext!);
        }

        // One save: the order, its reservation, the discount use, the cart, the charges and the messages.
        await orders.SaveAsync(order, EventPublishOption.InMemoryPublishOnly);

        if (command.PaymentMethod != OrderPaymentMethod.BankTransfer)
        {
            var invoice = await OrderBilling.IssueInvoiceAsync(dispatcher, order, command);
            order.AttachInvoice(invoice.InvoiceId, invoice.Number);
            await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        }

        return Success(OrderPlacement.ResultFor(order, account));
    }
}
