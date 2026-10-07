using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class SourcingOriginCreatedEvent : Event
{
    public required string CountryCode { get; init; }
}
