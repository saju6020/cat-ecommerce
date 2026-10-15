using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Order;

/// <summary>
/// HO-ORD-001 §11.1 C9 (US-ORD-02 to US-ORD-08, US-ORD-18 AC1) -- place the signed-in customer's order from their active cart. The caller is the
/// customer in <c>UserContext</c>; nothing here is a price or a total: the server recomputes the quote and refuses unless <see cref="QuoteFingerprint"/>
/// is the fingerprint of exactly what it computed (<c>409 CheckoutQuoteChanged</c>, ADR-200). <see cref="IdempotencyKey"/> is the request's
/// <c>Idempotency-Key</c>, kept on the order behind the HTTP edge's own guard (ADR-202).
/// </summary>
public sealed class PlaceOrderCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid IdempotencyKey { get; init; }
    public required FulfilmentMethod FulfilmentMethod { get; init; }
    public Guid? DeliveryAddressId { get; init; }
    public DeliveryServiceLevel? ServiceLevel { get; init; }
    public DateOnly? SlotDate { get; init; }
    public TimeOnly? SlotWindowStart { get; init; }
    public Guid? PickupBranchId { get; init; }
    public required OrderPaymentMethod PaymentMethod { get; init; }
    public string? DiscountCode { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public required string QuoteFingerprint { get; init; }
}

/// <summary>Where to send the money for a bank-transfer order (US-ORD-19 AC1): the shop's account and the reference to quote.</summary>
public sealed record TransferInstructionsResult(
    string AccountName, string AccountNumber, string BankName, string? BranchName, string? RoutingNumber, string PaymentReference, decimal AmountDue, string CurrencyCode);

/// <summary>The result of C9 -- <c>201</c> with the real outcome, never an acknowledgement (ADR-013).</summary>
public sealed record PlaceOrderResult(
    Guid OrderId, string OrderNumber, string Status, decimal GrandTotal, string CurrencyCode, DateTimeOffset? PaymentDueBy, TransferInstructionsResult? TransferInstructions);
