namespace CatEcommerce.Api.Contracts.Account;

public sealed class NotificationPreferenceChangeRequest
{
    public required string Channel { get; init; }
    public required string Category { get; init; }
    public required bool IsGranted { get; init; }
}

public sealed class ChangeNotificationPreferencesRequest
{
    public IReadOnlyList<NotificationPreferenceChangeRequest> Changes { get; init; } = [];

    /// <summary>HO-NTF-001 §12.4 amendment.</summary>
    public string? PreferredChannel { get; init; }
}
