using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-RPT-001 section 11.7 item 2 (HO-NTF-001 amendment) -- picks the one <see cref="INotificationAttachmentSource"/> for a kind. A kind with no source loads nothing, which cancels the notification.</summary>
public sealed class NotificationAttachmentResolver(IEnumerable<INotificationAttachmentSource> sources) : INotificationAttachmentResolver
{
    public Task<NotificationAttachment?> LoadAsync(NotificationAttachmentKind kind, Guid id, CancellationToken ct = default)
    {
        var source = sources.FirstOrDefault(s => s.Kind == kind);
        return source is null ? Task.FromResult<NotificationAttachment?>(null) : source.LoadAsync(id, ct);
    }
}
