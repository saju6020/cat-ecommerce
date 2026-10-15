using System.Globalization;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The reason codes of an unavailable payment option (HO-ORD-001 §8.8) -- stable strings the front end keys on.</summary>
public static class PaymentUnavailableReasons
{
    public const string NotForPickup = nameof(NotForPickup);
    public const string CodDisabled = nameof(CodDisabled);
    public const string CodNotOfferedInZone = nameof(CodNotOfferedInZone);
    public const string CodAboveOrderLimit = nameof(CodAboveOrderLimit);
    public const string BankTransferDisabled = nameof(BankTransferDisabled);
    public const string NotForDelivery = nameof(NotForDelivery);
    public const string PayOnCollectionDisabled = nameof(PayOnCollectionDisabled);
}

/// <summary>What <see cref="PaymentOptionPolicy"/> needs. <c>ZoneCodEnabled</c> is <c>null</c> for a pickup order (no zone). The ceiling is the
/// zone's own, else the branch setting (<c>orders.codMaxOrderValue</c>).</summary>
public sealed record PaymentOptionInputs(
    FulfilmentMethod Fulfilment,
    bool CodEnabledSetting,
    decimal CodMaxOrderValueSetting,
    bool? ZoneCodEnabled,
    decimal? ZoneCodMaxOrderValue,
    MinorUnits GrandTotal,
    bool BankTransferEnabledSetting,
    bool BankAccountActive,
    bool PayOnCollectionEnabledSetting,
    string CurrencyCode);

/// <summary>One payment option as the quote lists it: available, or hidden with its reason (US-ORD-07 AC2).</summary>
public sealed record PaymentOption(OrderPaymentMethod Method, bool Available, string? UnavailableReason, string? Message);

/// <summary>
/// HO-ORD-001 §8.8, US-ORD-07, BR-15 -- pure. Which of the three online payment methods is offered for a checkout, and for each one that is not,
/// the reason. Cash on delivery is for delivery orders only, switched on by the branch setting <b>and</b> the zone, and only up to a ceiling (the
/// zone's, else the setting); bank transfer needs the setting and an active bank account; pay on collection is for pickup orders only. The
/// counter's <c>InStore</c> method is never an online option. No gateway, no card (C-5).
/// </summary>
public static class PaymentOptionPolicy
{
    public static IReadOnlyList<PaymentOption> Evaluate(PaymentOptionInputs i) =>
    [
        CashOnDelivery(i),
        BankTransfer(i),
        PayOnCollection(i),
    ];

    public static PaymentOption For(PaymentOptionInputs inputs, OrderPaymentMethod method) =>
        Evaluate(inputs).FirstOrDefault(o => o.Method == method)
        ?? new PaymentOption(method, false, PaymentUnavailableReasons.NotForDelivery, "This payment method is not offered online.");

    private static PaymentOption CashOnDelivery(PaymentOptionInputs i)
    {
        if (i.Fulfilment != FulfilmentMethod.Delivery) return Unavailable(OrderPaymentMethod.CashOnDelivery, PaymentUnavailableReasons.NotForPickup, "Cash on delivery is for delivery orders only.");
        if (!i.CodEnabledSetting) return Unavailable(OrderPaymentMethod.CashOnDelivery, PaymentUnavailableReasons.CodDisabled, "Cash on delivery is not available right now.");
        if (i.ZoneCodEnabled != true) return Unavailable(OrderPaymentMethod.CashOnDelivery, PaymentUnavailableReasons.CodNotOfferedInZone, "Cash on delivery is not offered in your delivery area.");

        var ceiling = MinorUnits.FromAmount(i.ZoneCodMaxOrderValue ?? i.CodMaxOrderValueSetting);
        if (i.GrandTotal > ceiling)
            return Unavailable(OrderPaymentMethod.CashOnDelivery, PaymentUnavailableReasons.CodAboveOrderLimit,
                $"Cash on delivery is available for orders up to {i.CurrencyCode} {ceiling.ToAmount().ToString("N2", CultureInfo.InvariantCulture)}.");

        return new PaymentOption(OrderPaymentMethod.CashOnDelivery, true, null, null);
    }

    private static PaymentOption BankTransfer(PaymentOptionInputs i) =>
        i.BankTransferEnabledSetting && i.BankAccountActive
            ? new PaymentOption(OrderPaymentMethod.BankTransfer, true, null, null)
            : Unavailable(OrderPaymentMethod.BankTransfer, PaymentUnavailableReasons.BankTransferDisabled, "Bank transfer is not available right now.");

    private static PaymentOption PayOnCollection(PaymentOptionInputs i)
    {
        if (i.Fulfilment != FulfilmentMethod.Pickup) return Unavailable(OrderPaymentMethod.PayOnCollection, PaymentUnavailableReasons.NotForDelivery, "Pay on collection is for pickup orders only.");
        if (!i.PayOnCollectionEnabledSetting) return Unavailable(OrderPaymentMethod.PayOnCollection, PaymentUnavailableReasons.PayOnCollectionDisabled, "Pay on collection is not available right now.");

        return new PaymentOption(OrderPaymentMethod.PayOnCollection, true, null, null);
    }

    private static PaymentOption Unavailable(OrderPaymentMethod method, string reason, string message) => new(method, false, reason, message);
}
