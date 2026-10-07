using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §8.9/§12.2 -- added beyond §8.13's own port list (recorded as a deviation in the
/// implementation report) so C5 (an Application-layer handler) can resolve the sender configured
/// for a channel without depending on Infrastructure's own <c>NotificationSenderResolver</c> /
/// <c>NotificationOptions</c> types, which would invert the layering `.ai/architecture.md`
/// requires. Implemented by Infrastructure's <c>NotificationSenderResolver</c> (Singleton).
/// </summary>
public interface INotificationChannelSenderResolver
{
    INotificationChannelSender Resolve(NotificationChannel channel);
}
