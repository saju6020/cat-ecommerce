using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C6 -- sent by the worker. Exactly one of <see cref="RequestId"/> /
/// <see cref="NotificationId"/> is set.</summary>
public sealed class RedactNotificationSecretsCommand : Command
{
    public Guid? RequestId { get; init; }
    public Guid? NotificationId { get; init; }
}
