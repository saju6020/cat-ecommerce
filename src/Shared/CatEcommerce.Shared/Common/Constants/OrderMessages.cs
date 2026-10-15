namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ORD-001 §10.3/§10.4 -- the plain, actionable text (NFR-USA-4) of Ordering's refusals. Shared between an aggregate or handler
/// pre-check and the corresponding race path in <c>UniqueConstraintExceptionFilter</c> (HO-ACC-001 §20.6), so the two cannot drift.
/// </summary>
public static class OrderMessages
{
    /// <summary>US-ORD-09 AC2: "A delivered order cannot be packed."</summary>
    public static string InvalidTransition(string fromStateWords, string actionVerb) =>
        $"A {fromStateWords} order cannot be {actionVerb}.";

    /// <summary>US-ORD-12 AC2.</summary>
    public const string CancellationNotAvailable = "Packing has started, so this order can no longer be cancelled online. Please contact the shop.";

    public const string DuplicateSubmission = "This order was already submitted.";
    public const string CartChangedOnAnotherDevice = "Your cart changed on another device - please retry.";
    public const string DeliveryZoneNameAlreadyExists = "A delivery zone with this name already exists.";
    public const string CourierNameAlreadyExists = "A courier with this name already exists.";
    public const string DiscountCodeAlreadyExists = "A discount code with this code already exists.";
    public const string IdempotencyKeyRequired = "Send an Idempotency-Key header containing a UUID.";
    public const string IdempotencyKeyReused = "This Idempotency-Key was already used with a different request.";
    public const string RequestInProgress = "This request is already being processed. Please wait a moment.";

    public const string SkuNotPurchasable = "This item is not available to buy.";
    public const string SkuUnavailable = "This item is out of stock.";
    public const string CartFull = "Your cart is full. Remove an item before adding another.";
    public const string DuplicateCartLine = "Your cart already has this item for that cat.";
    public const string PrescriptionCatRequired = "Choose which cat this prescription food is for.";
    public const string PrescriptionNotAuthorised = "This food needs a current veterinary authorisation for the cat. Book a consultation to get one.";

    /// <summary>The commit-time stock race (the last unit went to someone else) -- the generic form; a planned refusal names the product.</summary>
    public const string StockNoLongerAvailable = "Sorry - something in your order has just sold out. Please review your order and place it again.";
    /// <summary>Counter sale to a mobile whose account is not Active and has no Active surviving account (backlog ORD 43; narrowed by FU1 D1: a Merged account is followed to its survivor): cashier-facing, not the customer's "log in or reset your password".</summary>
    public const string CounterMobileHeldByInactiveAccount = "This mobile number belongs to a customer record that was merged into a record that is now closed, or that has been closed itself, so a sale cannot be made to it here. Find the customer under Customers and use their current record, or ask a manager.";

    public static string StockNoLongerAvailableFor(string productName) =>
        $"Sorry - '{productName}' has just sold out. It has been removed from your order; please review and place it again.";

    public const string CheckoutQuoteChanged = "Something in your order changed while you were checking out. Please review it and place it again.";
    public const string PaymentMethodUnavailable = "That payment method is not available for this order.";
    public const string PolicyAcceptanceRequired = "Please accept the current terms before placing your order.";
    public const string RecipientPhoneRequired = "Enter a phone number the courier can call.";
    public const string NothingToPlace = "There is nothing in your order that can be placed.";

    public const string DeliveryTaxNotConfigured = "Delivery is temporarily unavailable. Please choose collection from a branch, or try again later.";
    public const string AddressOutsideServedZones = "We do not deliver to this address yet. You can collect your order from one of our branches instead.";
}
