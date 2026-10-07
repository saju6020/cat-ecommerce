using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §12.2. See <see cref="INotificationDeliverySettings"/>.</summary>
public sealed class NotificationDeliverySettings(IOptions<NotificationOptions> options) : INotificationDeliverySettings
{
    public int SendTimeoutSeconds => options.Value.Worker.SendTimeoutSeconds;
    public int LeaseSeconds => options.Value.Worker.LeaseSeconds;
}
