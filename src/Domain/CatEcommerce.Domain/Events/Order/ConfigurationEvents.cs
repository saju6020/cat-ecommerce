using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Order;

/// <summary>HO-ORD-001 §11.5 -- a delivery zone was created, edited or (de)activated. Ids and flags only (U-A8): no zone name, no district.</summary>
public sealed class DeliveryZoneChangedEvent : Event
{
    public required Guid DeliveryZoneId { get; init; }
    public required Guid BranchId { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>HO-ORD-001 §11.5 -- a discount code was created, edited or (de)activated. Ids and flags only (U-A8): not the code text.</summary>
public sealed class DiscountCodeChangedEvent : Event
{
    public required Guid DiscountCodeId { get; init; }
    public required bool IsActive { get; init; }
}
