namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §8.8 -- implemented in Infrastructure over the shared
/// <c>PublicSiteOptions.WebBaseUrl</c> (integrator instruction, 2026-09-27, post
/// HO-CAT-001-gates merge) and this area's own <c>NotificationOptions.ApiBaseUrl</c>.
///
/// HO-APT-001 §8.9 amendment: <see cref="CatCareLink"/> now points at the booking flow
/// (<c>{WebBaseUrl}/book?catId={catId}&amp;category={category}</c>) instead of the cat's portal
/// page, and takes the booking category to preselect (<c>"Vaccination"</c> or <c>"FollowUp"</c> --
/// HO-NTF-001's own <c>CareReminderSweep</c> now passes one). <see cref="AppointmentLink"/> and
/// <see cref="WaitlistClaimLink"/> are new.
/// </summary>
public interface INotificationLinkBuilder
{
    string PasswordResetLink(string token);
    string CatCareLink(Guid catId, string category);
    string UnsubscribeLink(string token);
    string OneClickUnsubscribeUrl(string token);

    /// <summary>HO-APT-001 §8.9 -- <c>{WebBaseUrl}/portal/appointments/{id}</c>. Used for
    /// <c>manageLink</c> on every appointment message.</summary>
    string AppointmentLink(Guid appointmentId);

    /// <summary>HO-APT-001 §8.9/§12.2 -- the waitlist offer's <c>claimLink</c>, equal to
    /// <see cref="AppointmentLink"/> of the held appointment the offer created.</summary>
    string WaitlistClaimLink(Guid heldAppointmentId) => AppointmentLink(heldAppointmentId);

    /// <summary>HO-APT-001 §8.9 -- <c>{WebBaseUrl}/book</c>. The cancellation notice's
    /// <c>bookLink</c> ("book a new appointment").</summary>
    string BookingLink();

    /// <summary>HO-SHP-001 §8.11 -- <c>{WebBaseUrl}/shop/products/{slug}?sku={skuId}</c>. Used for
    /// <c>productLink</c> on <c>shop.back-in-stock</c>.</summary>
    string ProductLink(string slug, Guid skuId);
}
