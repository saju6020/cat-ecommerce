using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class TaxClassCreatedEvent : Event
{
    public required Guid TaxClassId { get; init; }
}

public sealed class TaxClassUpdatedEvent : Event
{
    public required Guid TaxClassId { get; init; }
}

public sealed class TaxRateSetEvent : Event
{
    public required Guid TaxClassId { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}
