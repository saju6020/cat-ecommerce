using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-NTF-001 §4.3/§10.3 -- <see cref="Push"/> exists as a channel value but is never a
/// deliverable candidate in this slice (no provider, ADR-058 R9).</summary>
public static class NotificationChannelRules
{
    public static readonly IReadOnlyList<NotificationChannel> DeliverableChannels =
        [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp];

    public static bool IsDeliverable(NotificationChannel channel) => DeliverableChannels.Contains(channel);
}
