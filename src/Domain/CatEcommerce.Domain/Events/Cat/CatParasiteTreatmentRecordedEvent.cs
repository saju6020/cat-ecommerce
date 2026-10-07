using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatParasiteTreatmentRecordedEvent : Event
{
    public required Guid CatId { get; init; }
    public required Guid TreatmentId { get; init; }
    public required DateOnly NextDueOn { get; init; }
}
