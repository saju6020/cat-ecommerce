using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-ORD-001 §8.3 (US-ORD-09 AC2) -- one row of an order's status history: from (null on creation), to, when, by whom and an optional short
/// note (e.g. "Payment window expired"). Every transition appends exactly one; nothing is ever edited or removed.</summary>
public sealed class OrderStatusChange
{
    public Guid Id { get; private set; }
    public OrderStatus? From { get; private set; }
    public OrderStatus To { get; private set; } = default!;
    public DateTimeOffset ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }
    public string? Note { get; private set; }

    private OrderStatusChange() { }

    public OrderStatusChange(Guid id, OrderStatus? from, OrderStatus to, DateTimeOffset changedOn, Guid changedBy, string? note)
    {
        Id = id;
        From = from;
        To = to;
        ChangedOn = changedOn;
        ChangedBy = changedBy;
        Note = note;
    }
}
