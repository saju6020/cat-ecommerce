namespace CatEcommerce.Domain.Entities;

/// <summary>HO-ORD-001 §8.3 -- a reference from an order to a payment recorded in Billing (the money lives there): its id, the method name and amount, and
/// when it was recorded. <c>AmountPaid</c> on the order is the sum of these. No card number or token exists here (C-5).</summary>
public sealed class OrderPaymentRef
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public string Method { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }

    private OrderPaymentRef() { }

    public OrderPaymentRef(Guid id, Guid paymentId, string method, decimal amount, DateTimeOffset recordedOn)
    {
        Id = id;
        PaymentId = paymentId;
        Method = method;
        Amount = amount;
        RecordedOn = recordedOn;
    }
}
