using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-ACC-001 §8.6 / §10.2. Marketing is opt-in (<see cref="DefaultMarketingGranted"/> is
/// <c>false</c> -- NFR-LEG-4). Transactional notifications can never be turned off -- there is no
/// representation of "transactional: off" anywhere (FR-NTF-4 by construction).
/// </summary>
public static class NotificationPreferenceRules
{
    public const bool DefaultMarketingGranted = false;

    public static bool IsAlwaysOn(NotificationCategory category) =>
        category == NotificationCategory.Transactional;

    public static bool IsEditable(NotificationCategory category) =>
        category == NotificationCategory.Marketing;

    public static bool Permits(NotificationChannel channel, NotificationCategory category, bool marketingGranted) =>
        category == NotificationCategory.Transactional || marketingGranted;

    /// <summary>Every channel x category cell -- drives Q-E2 so the client never hardcodes which cells are locked.</summary>
    public static IEnumerable<(NotificationChannel Channel, NotificationCategory Category)> GetAll()
    {
        foreach (var channel in Enumeration.GetAll<NotificationChannel>())
            foreach (var category in Enumeration.GetAll<NotificationCategory>())
                yield return (channel, category);
    }
}
