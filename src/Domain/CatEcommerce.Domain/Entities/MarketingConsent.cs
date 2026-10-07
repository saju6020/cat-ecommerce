using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-ACC-001 §8.6. Owned by <c>CustomerNotificationPreferencesAggregate</c>. Only marketing
/// cells are ever stored -- there is no row representing "transactional: off".
/// </summary>
public sealed class MarketingConsent
{
    public Guid Id { get; private set; }
    public NotificationChannel Channel { get; private set; } = default!;
    public bool IsGranted { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }
    public string Source { get; private set; } = default!;

    private MarketingConsent() { }

    public MarketingConsent(Guid id, NotificationChannel channel, bool isGranted, DateTimeOffset recordedOn, string source)
    {
        Id = id;
        Channel = channel;
        IsGranted = isGranted;
        RecordedOn = recordedOn;
        Source = source;
    }

    internal void Update(bool isGranted, DateTimeOffset recordedOn, string source)
    {
        IsGranted = isGranted;
        RecordedOn = recordedOn;
        Source = source;
    }
}
