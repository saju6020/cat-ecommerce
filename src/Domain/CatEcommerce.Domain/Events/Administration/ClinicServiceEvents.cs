using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class ClinicServiceCreatedEvent : Event
{
    public required Guid ServiceId { get; init; }
}

public sealed class ClinicServiceUpdatedEvent : Event
{
    public required Guid ServiceId { get; init; }
}

public sealed class ClinicServiceActivationChangedEvent : Event
{
    public required Guid ServiceId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class ServicePriceSetEvent : Event
{
    public required Guid ServiceId { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}
