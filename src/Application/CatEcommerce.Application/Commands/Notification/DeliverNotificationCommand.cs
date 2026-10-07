using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C5 -- sent by the worker.</summary>
public sealed class DeliverNotificationCommand : Command
{
    public required Guid NotificationId { get; init; }
}
