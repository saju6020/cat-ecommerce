using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

/// <summary>Keys only -- values are captured by the audit interceptor with before/after
/// (HO-ADM-001 §8.7). Also the cache-invalidation signal for <c>CachedBusinessSettings</c>.</summary>
public sealed class BusinessSettingsChangedEvent : Event
{
    public required Guid ScopeId { get; init; }
    public required IReadOnlyList<string> Keys { get; init; }
}
