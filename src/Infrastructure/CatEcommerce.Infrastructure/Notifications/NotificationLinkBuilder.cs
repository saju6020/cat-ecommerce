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

    public string InvoiceLink(Guid invoiceId) => $"{Base()}/portal/billing/invoices/{invoiceId}";

    public string ReceiptLink(Guid paymentId) => $"{Base()}/portal/billing/payments/{paymentId}";

    public string OrderLink(Guid orderId) => $"{Base()}/portal/orders/{orderId}";

    public string ProductLink(string slug, Guid skuId) => $"{Base()}/shop/products/{Uri.EscapeDataString(slug)}?sku={skuId}";

    public string VisitSummaryLink(Guid visitId) => $"{Base()}/portal/visits/{visitId}";

    public string PrescriptionLink(Guid prescriptionId) => $"{Base()}/portal/prescriptions/{prescriptionId}";

    public string RepeatRequestLink(Guid prescriptionId) => $"{Base()}/portal/prescriptions/{prescriptionId}?action=repeat";

    public string SurgeryLink(Guid surgeryId) => $"{Base()}/portal/surgeries/{surgeryId}";

    public string FollowUpBookingLink(Guid catId) => CatCareLink(catId, "FollowUp");

    /// <summary>HO-RPT-001 section 11.7 item 7: the staff link to the report a scheduled email is about; <c>all</c> when the schedule covers every branch.</summary>
    public string ReportLink(string reportKey, DateOnly from, DateOnly to, Guid? branchId) =>
        $"{Base()}/staff/reports/{Uri.EscapeDataString(reportKey)}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&branchId={(branchId is { } id ? id.ToString() : "all")}";

    public string BoardingStayLink(Guid stayId) => $"{Base()}/portal/boarding/{stayId}";

    public string BoardingDiaryLink(Guid stayId) => $"{Base()}/portal/boarding/{stayId}/diary";

    public string BoardingIncidentLink(Guid stayId, Guid incidentId) => $"{Base()}/portal/boarding/{stayId}/incidents/{incidentId}";

    public string BoardingApprovalLink(Guid stayId, Guid approvalId) => $"{Base()}/portal/boarding/{stayId}/approvals/{approvalId}";

    public string BoardingSummaryLink(Guid stayId) => $"{Base()}/portal/boarding/{stayId}/summary";

    public string BoardingBookLink(DateOnly? arrivalOn = null, DateOnly? departureOn = null) =>
        arrivalOn is { } a && departureOn is { } d ? $"{Base()}/boarding/book?arrival={a:yyyy-MM-dd}&departure={d:yyyy-MM-dd}" : $"{Base()}/boarding/book";

    public string BoardingPaymentInstructionsLink() => $"{Base()}/pages/boarding-payment-instructions";

    public string DietAuthorisationsLink() => $"{Base()}/portal/prescriptions?tab=diets";

    public string UnsubscribeLink(string token) => $"{Base()}/unsubscribe#token={token}";

    public string OneClickUnsubscribeUrl(string token) => $"{options.Value.ApiBaseUrl.TrimEnd('/')}/api/notifications/unsubscribe/one-click?token={Uri.EscapeDataString(token)}";

    private string Base() => publicSiteOptions.Value.WebBaseUrl.TrimEnd('/');
}
