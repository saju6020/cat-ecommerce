using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-NTF-001 §8.5 -- the result of <c>INotificationRecipientDirectory.ResolveAsync</c>.
/// A merged customer resolves to the survivor; an anonymised customer or an inactive staff account
/// resolves with <see cref="IsActive"/> false.</summary>
public sealed record RecipientProfile(
    NotificationRecipientKind Kind,
    Guid Id,
    bool IsActive,
    string FullName,
    string? MobileNumber,
    string? EmailAddress,
    NotificationChannel EffectivePreferredChannel,
    Func<NotificationChannel, bool> MarketingGranted)
{
    public string? DestinationFor(NotificationChannel channel) =>
        channel == NotificationChannel.Sms ? MobileNumber : channel == NotificationChannel.Email ? EmailAddress : null;
}
