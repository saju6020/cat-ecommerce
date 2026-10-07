using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

/// <summary>HO-SHP-001 section 11.5 -- the product id and the locale CODE (e.g. <c>bn</c>), never the
/// translated text (U-X1).</summary>
public sealed class ProductTranslationChangedEvent : Event
{
    public required Guid ProductId { get; init; }
    public required string Locale { get; init; }
}
