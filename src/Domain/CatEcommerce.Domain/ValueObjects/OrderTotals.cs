namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ORD-001 §8.3 -- the money of one order, each component its own field (US-ORD-04 AC1), all <c>decimal(12,2)</c> in the order's one currency
/// (ADR-054, ADR-317). <c>GrandTotal = Subtotal - LineDiscountTotal - OrderDiscount + DeliveryCharge + TaxTotal</c> (FR-ORD-5); the aggregate re-sums in
/// minor units and refuses a mismatch -- a programming error, never a business outcome.
/// </summary>
public sealed record OrderTotals
{
    public decimal Subtotal { get; init; }
    public decimal LineDiscountTotal { get; init; }
    public decimal OrderDiscount { get; init; }
    public decimal DeliveryCharge { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal GrandTotal { get; init; }

    public OrderTotals(decimal subtotal, decimal lineDiscountTotal, decimal orderDiscount, decimal deliveryCharge, decimal taxTotal, decimal grandTotal)
    {
        Subtotal = subtotal;
        LineDiscountTotal = lineDiscountTotal;
        OrderDiscount = orderDiscount;
        DeliveryCharge = deliveryCharge;
        TaxTotal = taxTotal;
        GrandTotal = grandTotal;
    }

    private OrderTotals()
    {
    }
}
