using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class MaintenanceModeChangedEvent : Event
{
    public required bool MaintenanceMode { get; init; }
}
