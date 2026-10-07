using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatWeightRecordedEvent : Event
{
    public required Guid CatId { get; init; }
    public required Guid WeightRecordId { get; init; }
    public required decimal Kilograms { get; init; }
}
