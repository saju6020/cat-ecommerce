namespace CatEcommerce.Api.Contracts.Order;

/// <summary>HO-ORD-001 §12.4 -- the body of <c>POST /api/customers/me/orders</c>. Choices and the fingerprint the customer acknowledged; <b>never</b> a price, a
/// total, a customer id or a branch -- the server recomputes everything (ADR-200). The <c>Idempotency-Key</c> travels in the header (ADR-202).</summary>
public sealed class PlaceOrderRequest
{
    public string Fulfilment { get; init; } = default!;
    public Guid? DeliveryAddressId { get; init; }
    public string? ServiceLevel { get; init; }
    public string? SlotDate { get; init; }
    public string? SlotWindowStart { get; init; }
    public Guid? PickupBranchId { get; init; }
    public string PaymentMethod { get; init; } = default!;
    public string? DiscountCode { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string QuoteFingerprint { get; init; } = default!;
}
