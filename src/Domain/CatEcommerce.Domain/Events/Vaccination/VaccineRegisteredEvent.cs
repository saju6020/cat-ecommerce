using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Vaccination;

public sealed class VaccineRegisteredEvent : Event
{
    public required Guid VaccineId { get; init; }
    public required string Code { get; init; }
}
