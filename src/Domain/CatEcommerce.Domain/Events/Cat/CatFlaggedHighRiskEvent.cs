using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

/// <summary>Carries the actor (via inherited UserContext) and the reason — audited, US-CAT-04 AC2.</summary>
public sealed class CatFlaggedHighRiskEvent : Event
{
    public required Guid CatId { get; init; }
    public required string HandlingNote { get; init; }
    public required string Reason { get; init; }
}
