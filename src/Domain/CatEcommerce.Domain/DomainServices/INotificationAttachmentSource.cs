using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The file an attachment reference resolves to: loaded by its owner immediately before sending. Never logged, never written to <c>notify.*</c>.</summary>
public sealed record NotificationAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// HO-RPT-001 section 11.7 item 2 (HO-NTF-001 amendment, ADR-288) -- one implementation per <see cref="NotificationAttachmentKind"/>, owned by the area whose file it is. Read only; returns null when
/// the file no longer exists (a purged report), which cancels the notification rather than sending it without its attachment.
/// </summary>
public interface INotificationAttachmentSource
{
    NotificationAttachmentKind Kind { get; }

    Task<NotificationAttachment?> LoadAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Picks the <see cref="INotificationAttachmentSource"/> for a kind. Added so the Application-layer deliver handler depends on a Domain interface, not on Infrastructure.</summary>
public interface INotificationAttachmentResolver
{
    Task<NotificationAttachment?> LoadAsync(NotificationAttachmentKind kind, Guid id, CancellationToken ct = default);
}
