using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C9 -- sent by the public unsubscribe endpoints.</summary>
public sealed class UnsubscribeFromMarketingCommand : Command
{
    public required string Token { get; init; }
}
