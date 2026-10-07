using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Notification;

public sealed class PreviewTemplateContentItem
{
    public required string Channel { get; init; }
    public string? Subject { get; init; }
    public string? Title { get; init; }
    public required string Body { get; init; }
    public string? LinkPath { get; init; }
}

/// <summary>HO-NTF-001 §12.4/§12.6 -- POST /api/admin/notification-templates/{key}/preview.
/// Pure -- no repository beyond the catalogue lookup; writes nothing.</summary>
public sealed class PreviewNotificationTemplateQuery : Query<NotificationTemplatePreviewViewModel>
{
    public required string Key { get; init; }
    public required IReadOnlyList<PreviewTemplateContentItem> Contents { get; init; }
    public IReadOnlyDictionary<string, string>? SampleValues { get; init; }
}
