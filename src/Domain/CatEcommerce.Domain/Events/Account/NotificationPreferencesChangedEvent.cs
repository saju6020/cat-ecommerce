using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

/// <summary>Carries display names and before/after booleans -- not personal data.</summary>
public sealed class NotificationPreferencesChangedEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required IReadOnlyList<string> Changes { get; init; }
}
