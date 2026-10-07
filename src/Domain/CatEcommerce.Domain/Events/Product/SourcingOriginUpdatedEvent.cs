using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class SourcingOriginUpdatedEvent : Event
{
    public required string CountryCode { get; init; }
}
