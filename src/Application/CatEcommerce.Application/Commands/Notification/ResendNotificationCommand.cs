using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C7 -- sent by staff. <see cref="Channel"/> null means "same channel".</summary>
public sealed class ResendNotificationCommand : Command
{
    public required Guid NotificationId { get; init; }
    public required Guid NewNotificationId { get; init; }
    public string? Channel { get; init; }
}

/// <summary>Gate fix 2026-09-27 (ARCHITECTURE LOW): §12.4 specifies the resend response as
/// <c>{ notificationId, status, notBefore? }</c>, not just the new id.</summary>
public sealed record ResendNotificationResult(Guid NotificationId, string Status, DateTimeOffset? NotBefore);
