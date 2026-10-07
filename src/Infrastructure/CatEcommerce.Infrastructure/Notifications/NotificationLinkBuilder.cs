using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>
/// HO-NTF-001 §8.8. <see cref="CatCareLink"/> is amended by HO-APT-001 §8.9 to point at the booking
/// flow; <see cref="AppointmentLink"/> is new.
///
/// Integrator instruction (2026-09-27, post HO-CAT-001-gates merge): the web base URL is now the
/// ONE shared <see cref="PublicSiteOptions.WebBaseUrl"/> (also used for the cage-card QR code,
/// US-CAT-11), not this area's own <c>NotificationOptions.WebBaseUrl</c> (removed). The api base
/// URL has no shared equivalent and stays <see cref="NotificationOptions.ApiBaseUrl"/>.
/// </summary>
public sealed class NotificationLinkBuilder(IOptions<NotificationOptions> options, IOptions<PublicSiteOptions> publicSiteOptions) : INotificationLinkBuilder
{
    public string PasswordResetLink(string token) => $"{Base()}/reset-password#token={token}";

    public string CatCareLink(Guid catId, string category) => $"{Base()}/book?catId={catId}&category={Uri.EscapeDataString(category)}";

    public string AppointmentLink(Guid appointmentId) => $"{Base()}/portal/appointments/{appointmentId}";

    public string BookingLink() => $"{Base()}/book";

    public string ProductLink(string slug, Guid skuId) => $"{Base()}/shop/products/{Uri.EscapeDataString(slug)}?sku={skuId}";

    public string UnsubscribeLink(string token) => $"{Base()}/unsubscribe#token={token}";

    public string OneClickUnsubscribeUrl(string token) => $"{options.Value.ApiBaseUrl.TrimEnd('/')}/api/notifications/unsubscribe/one-click?token={Uri.EscapeDataString(token)}";

    private string Base() => publicSiteOptions.Value.WebBaseUrl.TrimEnd('/');
}
