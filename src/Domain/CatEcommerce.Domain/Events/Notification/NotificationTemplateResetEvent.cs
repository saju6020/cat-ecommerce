using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Notification;

/// <summary>HO-NTF-001 §11.5.</summary>
public sealed class NotificationTemplateResetEvent : Event
{
    public required string Key { get; init; }
    public required int ContentVersion { get; init; }
}
