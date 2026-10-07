using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

/// <summary>HO-SHP-001 section 11.5 -- ids only (U-X1).</summary>
public sealed class StockAlertRequestedEvent : Event
{
    public required Guid StockAlertId { get; init; }
    public required Guid SkuId { get; init; }
}
