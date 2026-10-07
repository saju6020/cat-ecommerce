using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C2.</summary>
public sealed class ResetNotificationTemplateCommand : Command
{
    public required string Key { get; init; }
    public required int ExpectedContentVersion { get; init; }
}
