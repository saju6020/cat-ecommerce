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

    /// <summary>HO-EMR-001 §8.8 -- <c>{WebBaseUrl}/portal/visits/{visitId}</c>. The <c>summaryLink</c> of
    /// <c>visit.summary-available</c>: the link carries the owner to the portal, where access is
    /// authenticated and logged -- no clinical content travels in the message.</summary>
    string VisitSummaryLink(Guid visitId);
    /// <summary>HO-BIL-001 §8.12 -- <c>{WebBaseUrl}/portal/billing/invoices/{id}</c>. Used for <c>invoiceLink</c> on
    /// <c>billing.invoice-issued</c>.</summary>
    string InvoiceLink(Guid invoiceId);

    /// <summary>HO-BIL-001 §8.12 -- <c>{WebBaseUrl}/portal/billing/payments/{id}</c>. Used for <c>receiptLink</c> on
    /// <c>billing.payment-received</c>.</summary>
    string ReceiptLink(Guid paymentId);

    /// <summary>HO-ORD-001 §8.12 -- <c>{WebBaseUrl}/portal/orders/{orderId}</c>. Used for <c>orderLink</c> on every <c>order.*</c> message.</summary>
    string OrderLink(Guid orderId);
    /// <summary>HO-PHM-001 §8.11 -- <c>{WebBaseUrl}/portal/prescriptions/{id}</c>. The <c>prescriptionLink</c> of <c>pharmacy.repeat-ready</c>.</summary>
    string PrescriptionLink(Guid prescriptionId);

    /// <summary>HO-PHM-001 §8.11 -- <c>{WebBaseUrl}/portal/prescriptions/{id}?action=repeat</c>. The direct reorder link of <c>pharmacy.refill-due</c>.</summary>
    string RepeatRequestLink(Guid prescriptionId);

    /// <summary>HO-PHM-001 §8.11 -- <c>{WebBaseUrl}/portal/prescriptions?tab=diets</c>. The <c>authorisationsLink</c> of <c>pharmacy.diet-authorised</c>.</summary>
    string DietAuthorisationsLink();

    /// <summary>HO-SRG-001 §8.10 -- <c>{WebBaseUrl}/portal/surgeries/{id}</c>. The <c>detailsLink</c> of every <c>surgery.*</c> message about a surgery. A default
    /// member (like <see cref="WaitlistClaimLink"/>) so an existing implementation of this port keeps compiling; the real builder overrides it.</summary>
    string SurgeryLink(Guid surgeryId) => throw new NotSupportedException("This link builder does not build surgery links.");

    /// <summary>HO-SRG-001 §8.10 -- <c>{WebBaseUrl}/book?catId={catId}&amp;category=FollowUp</c>. The <c>bookingLink</c> of <c>surgery.follow-up-due</c>.</summary>
    string FollowUpBookingLink(Guid catId) => CatCareLink(catId, "FollowUp");

    /// <summary>HO-RPT-001 section 11.7: a staff link to the report the scheduled email is about. branchId null means every branch.</summary>
    string ReportLink(string reportKey, DateOnly from, DateOnly to, Guid? branchId) => throw new NotSupportedException("This link builder does not build report links.");

    // HO-BRD-001 section 8.19 -- the seven boarding links. Default members so no other implementer changes.
    string BoardingStayLink(Guid stayId) => throw new NotSupportedException("This link builder does not build boarding links.");
    string BoardingDiaryLink(Guid stayId) => throw new NotSupportedException("This link builder does not build boarding links.");
    string BoardingIncidentLink(Guid stayId, Guid incidentId) => throw new NotSupportedException("This link builder does not build boarding links.");
    string BoardingApprovalLink(Guid stayId, Guid approvalId) => throw new NotSupportedException("This link builder does not build boarding links.");
    string BoardingSummaryLink(Guid stayId) => throw new NotSupportedException("This link builder does not build boarding links.");
    string BoardingBookLink(DateOnly? arrivalOn = null, DateOnly? departureOn = null) => throw new NotSupportedException("This link builder does not build boarding links.");
    string BoardingPaymentInstructionsLink() => throw new NotSupportedException("This link builder does not build boarding links.");
}
