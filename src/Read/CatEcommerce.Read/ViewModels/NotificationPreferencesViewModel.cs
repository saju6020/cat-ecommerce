namespace CatEcommerce.Read.ViewModels;

public sealed record NotificationPreferenceCellViewModel(
    string Channel, string Category, bool IsGranted, bool IsEditable, bool IsAlwaysOn);

/// <summary>HO-NTF-001 §12.4 amendment: three preferred-channel fields appended to HO-ACC-001's
/// Q-E2. <see cref="AvailablePreferredChannels"/> lists the channels the account holds an
/// identifier for.</summary>
public sealed record NotificationPreferencesViewModel(
    IReadOnlyList<NotificationPreferenceCellViewModel> Cells,
    string? PreferredChannel = null,
    string? EffectivePreferredChannel = null,
    IReadOnlyList<string>? AvailablePreferredChannels = null);
