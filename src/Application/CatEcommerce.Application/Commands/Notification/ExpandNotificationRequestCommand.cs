using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C4 -- sent by the worker.</summary>
public sealed class ExpandNotificationRequestCommand : Command
{
    public required Guid RequestId { get; init; }
}
