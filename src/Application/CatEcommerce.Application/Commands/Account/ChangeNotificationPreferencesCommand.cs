using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class NotificationPreferenceChangeItem
{
    public required string Channel { get; init; }
    public required string Category { get; init; }
    public required bool IsGranted { get; init; }
}

public sealed class ChangeNotificationPreferencesCommand : Command
{
    public required IReadOnlyList<NotificationPreferenceChangeItem> Changes { get; init; }

    /// <summary>HO-NTF-001 §8.5/§11.1 (amended). "Sms" or "Email" only; null leaves the stored
    /// preference unchanged.</summary>
    public string? PreferredChannel { get; init; }
}
