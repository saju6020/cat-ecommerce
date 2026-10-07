namespace CatEcommerce.Api.Contracts.Notification;

public sealed class NotificationTemplateContentRequest
{
    public required string Channel { get; init; }
    public string? Subject { get; init; }
    public string? Title { get; init; }
    public required string Body { get; init; }
    public string? LinkPath { get; init; }
}

public sealed class SaveNotificationTemplateRequest
{
    public required int ExpectedContentVersion { get; init; }
    public required IReadOnlyList<NotificationTemplateContentRequest> Contents { get; init; }
}

public sealed class ResetNotificationTemplateRequest
{
    public required int ExpectedContentVersion { get; init; }
}

public sealed class PreviewNotificationTemplateRequest
{
    public required IReadOnlyList<NotificationTemplateContentRequest> Contents { get; init; }
    public IReadOnlyDictionary<string, string>? SampleValues { get; init; }
}
