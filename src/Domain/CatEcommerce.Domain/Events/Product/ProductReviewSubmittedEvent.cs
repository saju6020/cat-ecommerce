using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class ProductReviewSubmittedEvent : Event
{
    public required Guid ReviewId { get; init; }
    public required Guid ProductId { get; init; }
}
