using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C8 -- sent by a customer; the customer id comes from <c>UserContext</c>.</summary>
public sealed class MarkInboxItemReadCommand : Command
{
    public required Guid NotificationId { get; init; }
}
