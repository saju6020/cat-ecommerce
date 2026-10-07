using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

/// <summary>HO-SHP-001 section 11.5. Ids, enums and numbers only -- no name, description or
/// ingredient text (U-X1).</summary>
public sealed class ProductCreatedEvent : Event
{
    public required Guid ProductId { get; init; }
    public required int Kind { get; init; }
    public required int Category { get; init; }
}
