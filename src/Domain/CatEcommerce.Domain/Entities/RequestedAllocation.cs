namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BIL-001 §8.8 -- an <c>(InvoiceId, Amount)</c> captured while a payment waits for
/// approval, applied (each capped at the invoice's outstanding at that time) when it is approved and
/// then cleared. A transient working set, not a financial record -- <c>AggregateOnly</c> in
/// <c>RecordClassification</c>.</summary>
public sealed class RequestedAllocation
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }

    private RequestedAllocation() { }

    public RequestedAllocation(Guid id, Guid invoiceId, decimal amount)
    {
        Id = id;
        InvoiceId = invoiceId;
        Amount = amount;
    }
}
