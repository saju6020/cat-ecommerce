using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatVaccinationRecordedEvent : Event
{
    public required Guid CatId { get; init; }
    public required Guid VaccinationId { get; init; }
    public required Guid VaccineId { get; init; }
    public required DateOnly NextDueOn { get; init; }
}
