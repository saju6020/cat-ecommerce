using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §11.4 -- the steps placement shares with the counter sale (C26) and HO-SUB-001's future command, as a static helper over the ports (not a
/// service layer, ADR-015; the same shape as HO-APT-001's <c>SlotClaim</c>): turning a priced quote into the order's snapshot, and turning a checkout
/// refusal into the answer. The <i>orchestration</i> -- what is saved, tracked or staged -- stays in each handler, where §11.4 lists it.
/// </summary>
public static class OrderPlacement
{
    /// <summary>The fulfilment snapshot of an online order: the recipient, the address as the customer had it, the zone / level / slot it was priced with,
    /// or the pickup branch (ADR-206).</summary>
    public static FulfilmentDetails FulfilmentFor(CheckoutInputs inputs, CheckoutQuote quote, string recipientName, string? recipientPhone)
    {
        if (inputs.Choice.Method == FulfilmentMethod.Pickup)
            return new FulfilmentDetails(recipientName, recipientPhone, null, null, null, null, null, null, null, null, null, null, null, null, null, inputs.Choice.PickupBranchId);

        var address = inputs.Address!;
        DeliverySlot? slot = null;
        if (inputs.Choice.SlotDate is { } date && inputs.Choice.SlotWindowStart is { } start)
            slot = quote.Delivery.ServiceLevels.FirstOrDefault(l => l.Level == quote.SelectedServiceLevel)?.Slots.FirstOrDefault(s => s.Date == date && s.WindowStart == start);

        return new FulfilmentDetails(
            recipientName, recipientPhone, address.Line1, address.Line2, address.City, address.District, address.PostalCode, address.DeliveryInstructions,
            quote.Delivery.ZoneId, quote.Delivery.ZoneName, quote.SelectedServiceLevel, slot?.Date, slot?.Label, slot?.WindowStart, slot?.WindowEnd, null);
    }

    /// <summary>The checkout refusal for the first blocking reason of a quote that cannot be placed (§8.7 rule 4).</summary>
    public static CommandResponse RefusalFor(CheckoutQuote quote)
    {
        var code = quote.BlockingReasons.FirstOrDefault() ?? BusinessViolationCodes.NothingToPlace;
        return Violation(code, PropertyFor(code), MessageFor(code));
    }

    private static string PropertyFor(string code) => code switch
    {
        BusinessViolationCodes.PaymentMethodUnavailable => "PaymentMethod",
        BusinessViolationCodes.DeliveryTaxNotConfigured => "FulfilmentMethod",
        BusinessViolationCodes.DeliveryAddressNotFound or BusinessViolationCodes.AddressOutsideServedZones => "DeliveryAddressId",
        BusinessViolationCodes.ServiceLevelUnavailable => "ServiceLevel",
        BusinessViolationCodes.DeliverySlotUnavailable => "SlotDate",
        BusinessViolationCodes.PickupBranchUnavailable => "PickupBranchId",
        _ => "Lines",
    };

    private static string MessageFor(string code) => code switch
    {
        BusinessViolationCodes.PrescriptionCatRequired => OrderMessages.PrescriptionCatRequired,
        BusinessViolationCodes.AddressOutsideServedZones => OrderMessages.AddressOutsideServedZones,
        BusinessViolationCodes.DeliveryTaxNotConfigured => OrderMessages.DeliveryTaxNotConfigured,
        BusinessViolationCodes.PaymentMethodUnavailable => OrderMessages.PaymentMethodUnavailable,
        BusinessViolationCodes.PolicyAcceptanceRequired => OrderMessages.PolicyAcceptanceRequired,
        BusinessViolationCodes.DeliveryAddressNotFound => "Choose a delivery address from your address book.",
        BusinessViolationCodes.ServiceLevelUnavailable => "That delivery service is not available for this address.",
        BusinessViolationCodes.DeliverySlotUnavailable => "That delivery slot is not available. Please choose another.",
        BusinessViolationCodes.PickupBranchUnavailable => "That branch is not available for collection.",
        BusinessViolationCodes.CartEmpty => "Your cart is empty.",
        _ => OrderMessages.NothingToPlace,
    };

    public static PlaceOrderResult ResultFor(OrderAggregate order, BankTransferAccountAggregate? account) =>
        new(order.Id, order.OrderNumber, order.Status.DisplayName, order.Totals.GrandTotal, order.CurrencyCode, order.PaymentDueBy,
            account is null || order.PaymentMethod != OrderPaymentMethod.BankTransfer
                ? null
                : new TransferInstructionsResult(
                    account.AccountName, account.AccountNumber, account.BankName, account.BranchName, account.RoutingNumber, order.OrderNumber,
                    order.Totals.GrandTotal, order.CurrencyCode));
}
