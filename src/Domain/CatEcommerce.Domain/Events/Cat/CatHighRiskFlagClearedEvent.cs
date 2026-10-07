using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatHighRiskFlagClearedEvent : Event
{
    public required Guid CatId { get; init; }
    public required string Reason { get; init; }
}
