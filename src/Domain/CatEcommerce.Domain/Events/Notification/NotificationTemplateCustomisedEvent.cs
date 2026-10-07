using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Notification;

/// <summary>HO-NTF-001 §11.5 -- key and version only, no wording (U-X1).</summary>
public sealed class NotificationTemplateCustomisedEvent : Event
{
    public required string Key { get; init; }
    public required int ContentVersion { get; init; }
}
