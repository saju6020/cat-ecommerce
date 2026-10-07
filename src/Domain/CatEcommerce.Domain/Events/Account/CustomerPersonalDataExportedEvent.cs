using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerPersonalDataExportedEvent : Event
{
    public required Guid CustomerId { get; init; }
}
