using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

public sealed class NotificationTemplateContentItem
{
    public required string Channel { get; init; }
    public string? Subject { get; init; }
    public string? Title { get; init; }
    public required string Body { get; init; }
    public string? LinkPath { get; init; }
}

/// <summary>HO-NTF-001 §11.1 C1.</summary>
public sealed class SaveNotificationTemplateCommand : Command
{
    public required string Key { get; init; }
    public required int ExpectedContentVersion { get; init; }
    public required IReadOnlyList<NotificationTemplateContentItem> Contents { get; init; }
}
