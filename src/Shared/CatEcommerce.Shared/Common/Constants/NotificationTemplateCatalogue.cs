using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-NTF-001 §8.2. A single producer-supplied variable a template definition declares.</summary>
public sealed record NotificationTemplateVariableDefinition(
    string Name,
    string Description,
    TemplateVariableKind Kind,
    string Sample,
    IReadOnlyList<NotificationChannel> RequiredOn);

/// <summary>The shipped wording for one channel of a definition.</summary>
public sealed record NotificationTemplateChannelDefault(
    NotificationChannel Channel,
    string? Subject,
    string? Title,
    string Body,
    string? LinkPath);

/// <summary>
/// One row of the code-defined template catalogue (HO-NTF-001 §8.2). What exists is code; what it
/// says is data -- the actual wording lives on <c>NotificationTemplateAggregate</c> once customised.
/// </summary>
public sealed record NotificationTemplateDefinition(
    string Key,
    string DisplayName,
    string Description,
    NotificationCategory Category,
    NotificationUrgency Urgency,
    IReadOnlyList<NotificationChannel> Channels,
    bool ContainsSecret,
    IReadOnlyList<NotificationTemplateVariableDefinition> Variables,
    IReadOnlyList<NotificationTemplateChannelDefault> Defaults)
{
    public NotificationTemplateChannelDefault? DefaultFor(NotificationChannel channel) =>
        Defaults.FirstOrDefault(d => d.Channel == channel);
}

/// <summary>One channel's wording, as supplied by a command or the aggregate's stored contents --
/// the shape <c>TemplateRules.Validate</c> checks (HO-NTF-001 §8.2/§11.1 C1).</summary>
public sealed record TemplateContentInput(
    NotificationChannel Channel,
    string? Subject,
    string? Title,
    string Body,
    string? LinkPath);

/// <summary>
/// HO-NTF-001 §8.2 -- the shipped definitions. Later areas append their own (§1 Amended); none are
/// built by this pass beyond the three this handover itself owns (reset, vaccination, deworming).
/// </summary>
public static partial class NotificationTemplateCatalogue
{
    public const string PasswordReset = "account.password-reset";
    public const string VaccinationDue = "care.vaccination-due";
    public const string DewormingDue = "care.deworming-due";

    /// <summary>HO-APT-001 §8.9 -- five templates appended by Clinic Appointments (ADR-074). All
    /// three channels, like the two care-reminder templates above.</summary>
    public const string AppointmentConfirmed = "appointment.confirmed";
    public const string AppointmentReminder = "appointment.reminder";
    public const string AppointmentRescheduled = "appointment.rescheduled";
    public const string AppointmentCancelled = "appointment.cancelled";
    public const string WaitlistSlotOffered = "waitlist.slot-offered";

    /// <summary>HO-SHP-001 §8.11 -- a <b>Marketing</b> template (HO-NTF-001 §4.2): a customer who asked
    /// for an alert but granted no marketing consent on any channel is told nothing (F33, OQ-2);
    /// the alert response's <c>deliverability</c> says so.</summary>
    public const string ShopBackInStock = "shop.back-in-stock";

    /// <summary>HO-EMR-001 §8.8/§8.9 (ADR-130, ADR-131) -- two Transactional templates. Neither carries
    /// clinical content: the summary message is link-only, and a follow-up reminder never states a reason
    /// (a follow-up reason is clinical information in an SMS).</summary>
    public const string VisitSummaryAvailable = "visit.summary-available";
    public const string VisitFollowUpDue = "visit.follow-up-due";
    /// <summary>HO-IMP-001 §8.12 -- a recalled batch's customer notice (<c>BatchRecallGuard</c>).</summary>
    public const string InventoryBatchRecall = "inventory.batch-recall";
    /// <summary>HO-BIL-001 §8.12 (ADR-193) -- three Transactional templates ("payment" messages are
    /// exempt from marketing opt-out, FR-NTF-4). Enqueued by command handlers only.</summary>
    public const string BillingInvoiceIssued = "billing.invoice-issued";
    public const string BillingPaymentReceived = "billing.payment-received";
    public const string BillingRefundIssued = "billing.refund-issued";

    /// <summary>HO-PHM-001 §8.11 (ADR-150) -- five Transactional templates (medical, FR-NTF-4). None carries a dose, a diagnosis or a batch
    /// number. Enqueued by command handlers (or, for the refill reminder, the timed producer), never by an event handler.</summary>
    public const string PharmacyRefillDue = "pharmacy.refill-due";
    public const string PharmacyRepeatReady = "pharmacy.repeat-ready";
    public const string PharmacyRepeatDeclined = "pharmacy.repeat-declined";
    public const string PharmacyDietAuthorised = "pharmacy.diet-authorised";
    public const string PharmacyDietAuthorisationExpired = "pharmacy.diet-authorisation-expired";

    public static readonly IReadOnlyList<NotificationTemplateDefinition> Definitions =
    [
        new NotificationTemplateDefinition(
            PasswordReset, "Password reset", "Sent when a customer or staff member requests a password reset link.",
            NotificationCategory.Transactional, NotificationUrgency.Urgent,
            [NotificationChannel.Sms, NotificationChannel.Email], ContainsSecret: true,
            Variables:
            [
                new NotificationTemplateVariableDefinition("resetLink", "The one-time reset link.", TemplateVariableKind.Url, "https://portal.example.com/reset-password#token=sample", [NotificationChannel.Sms, NotificationChannel.Email]),
                new NotificationTemplateVariableDefinition("expiresInMinutes", "Minutes until the link expires.", TemplateVariableKind.Text, "15", []),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "Reset your PurrCare password: {{resetLink}} (expires in {{expiresInMinutes}} minutes). If you did not request this, ignore this message.", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Reset your PurrCare password", null,
                    "Hello {{recipientName}},\n\nWe received a request to reset your PurrCare password. [Reset your password]({{resetLink}}). This link expires in {{expiresInMinutes}} minutes.\n\nIf you did not request this, you can safely ignore this message.", null),
            ]),

        new NotificationTemplateDefinition(
            VaccinationDue, "Vaccination due", "Sent ahead of a cat's vaccination due date.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("vaccineName", "The vaccine's name.", TemplateVariableKind.Text, "Rabies", []),
                new NotificationTemplateVariableDefinition("dueOn", "The due date.", TemplateVariableKind.Date, "2026-10-12", []),
                new NotificationTemplateVariableDefinition("bookingLink", "A direct booking link.", TemplateVariableKind.Url, "https://portal.example.com/portal/cats/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s {{vaccineName}} vaccination is due on {{dueOn}}. Book here: {{bookingLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "A vaccination is coming up for {{catName}}", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s {{vaccineName}} vaccination is due on {{dueOn}}. [Book now]({{bookingLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Vaccination due soon",
                    "{{catName}}'s {{vaccineName}} vaccination is due on {{dueOn}}.", "{{bookingLink}}"),
            ]),

        new NotificationTemplateDefinition(
            DewormingDue, "Deworming due", "Sent ahead of a cat's parasite treatment due date.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("treatmentName", "The treatment's name.", TemplateVariableKind.Text, "Deworming", []),
                new NotificationTemplateVariableDefinition("dueOn", "The due date.", TemplateVariableKind.Date, "2026-10-12", []),
                new NotificationTemplateVariableDefinition("bookingLink", "A direct booking link.", TemplateVariableKind.Url, "https://portal.example.com/portal/cats/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s {{treatmentName}} treatment is due on {{dueOn}}. Book here: {{bookingLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "A parasite treatment is coming up for {{catName}}", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s {{treatmentName}} treatment is due on {{dueOn}}. [Book now]({{bookingLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Treatment due soon",
                    "{{catName}}'s {{treatmentName}} treatment is due on {{dueOn}}.", "{{bookingLink}}"),
            ]),

        new NotificationTemplateDefinition(
            AppointmentConfirmed, "Appointment confirmed", "Sent the moment an appointment is confirmed (HO-APT-001 §8.9).",
            NotificationCategory.Transactional, NotificationUrgency.Urgent,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("serviceName", "The booked service.", TemplateVariableKind.Text, "Annual vaccination", []),
                new NotificationTemplateVariableDefinition("startsOnLocal", "The appointment's start time, in the branch's local time.", TemplateVariableKind.Text, "Mon 12 Oct 2026, 10:30", []),
                new NotificationTemplateVariableDefinition("performerName", "The performing vet or nurse.", TemplateVariableKind.Text, "Dr Rahman", []),
                new NotificationTemplateVariableDefinition("branchName", "The branch's name.", TemplateVariableKind.Text, "Gulshan Clinic", []),
                new NotificationTemplateVariableDefinition("branchAddress", "The branch's address.", TemplateVariableKind.Text, "House 12, Road 5, Gulshan", []),
                new NotificationTemplateVariableDefinition("manageLink", "A link to manage the appointment.", TemplateVariableKind.Url, "https://portal.example.com/portal/appointments/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "Your {{serviceName}} appointment for {{catName}} is confirmed for {{startsOnLocal}} with {{performerName}} at {{branchName}}. Manage: {{manageLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Your appointment is confirmed", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s {{serviceName}} appointment is confirmed for {{startsOnLocal}} with {{performerName}} at {{branchName}} ({{branchAddress}}). [Manage your appointment]({{manageLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Appointment confirmed",
                    "{{catName}}'s {{serviceName}} appointment is confirmed for {{startsOnLocal}}.", "{{manageLink}}"),
            ]),

        new NotificationTemplateDefinition(
            AppointmentReminder, "Appointment reminder", "Sent ahead of a booked appointment (HO-APT-001 §8.9).",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("serviceName", "The booked service.", TemplateVariableKind.Text, "Annual vaccination", []),
                new NotificationTemplateVariableDefinition("startsOnLocal", "The appointment's start time, in the branch's local time.", TemplateVariableKind.Text, "Mon 12 Oct 2026, 10:30", []),
                new NotificationTemplateVariableDefinition("performerName", "The performing vet or nurse.", TemplateVariableKind.Text, "Dr Rahman", []),
                new NotificationTemplateVariableDefinition("branchName", "The branch's name.", TemplateVariableKind.Text, "Gulshan Clinic", []),
                new NotificationTemplateVariableDefinition("branchAddress", "The branch's address.", TemplateVariableKind.Text, "House 12, Road 5, Gulshan", []),
                new NotificationTemplateVariableDefinition("manageLink", "A link to manage the appointment.", TemplateVariableKind.Url, "https://portal.example.com/portal/appointments/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
                new NotificationTemplateVariableDefinition("hoursBefore", "How many hours before the appointment this reminder is sent.", TemplateVariableKind.Text, "24", []),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "Reminder: {{catName}}'s {{serviceName}} appointment is at {{startsOnLocal}} with {{performerName}} at {{branchName}} ({{hoursBefore}}h to go). Manage: {{manageLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Reminder: your appointment is coming up", null,
                    "Hello {{recipientName}},\n\nThis is a reminder that {{catName}}'s {{serviceName}} appointment is at {{startsOnLocal}} with {{performerName}} at {{branchName}} ({{branchAddress}}), {{hoursBefore}} hours from now. [Manage your appointment]({{manageLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Appointment reminder",
                    "{{catName}}'s {{serviceName}} appointment is at {{startsOnLocal}} ({{hoursBefore}}h to go).", "{{manageLink}}"),
            ]),

        new NotificationTemplateDefinition(
            AppointmentRescheduled, "Appointment rescheduled", "Sent when an appointment's time changes (HO-APT-001 §8.9).",
            NotificationCategory.Transactional, NotificationUrgency.Urgent,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("serviceName", "The booked service.", TemplateVariableKind.Text, "Annual vaccination", []),
                new NotificationTemplateVariableDefinition("startsOnLocal", "The new start time, in the branch's local time.", TemplateVariableKind.Text, "Mon 12 Oct 2026, 10:30", []),
                new NotificationTemplateVariableDefinition("performerName", "The performing vet or nurse.", TemplateVariableKind.Text, "Dr Rahman", []),
                new NotificationTemplateVariableDefinition("branchName", "The branch's name.", TemplateVariableKind.Text, "Gulshan Clinic", []),
                new NotificationTemplateVariableDefinition("branchAddress", "The branch's address.", TemplateVariableKind.Text, "House 12, Road 5, Gulshan", []),
                new NotificationTemplateVariableDefinition("manageLink", "A link to manage the appointment.", TemplateVariableKind.Url, "https://portal.example.com/portal/appointments/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
                new NotificationTemplateVariableDefinition("previousStartsOnLocal", "The previous start time, in the branch's local time.", TemplateVariableKind.Text, "Mon 12 Oct 2026, 09:00", []),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s {{serviceName}} appointment moved from {{previousStartsOnLocal}} to {{startsOnLocal}} with {{performerName}} at {{branchName}}. Manage: {{manageLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Your appointment has been rescheduled", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s {{serviceName}} appointment moved from {{previousStartsOnLocal}} to {{startsOnLocal}} with {{performerName}} at {{branchName}} ({{branchAddress}}). [Manage your appointment]({{manageLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Appointment rescheduled",
                    "{{catName}}'s {{serviceName}} appointment moved from {{previousStartsOnLocal}} to {{startsOnLocal}}.", "{{manageLink}}"),
            ]),

        new NotificationTemplateDefinition(
            AppointmentCancelled, "Appointment cancelled", "Sent when an appointment is cancelled, by either side (HO-APT-001 §8.9).",
            NotificationCategory.Transactional, NotificationUrgency.Urgent,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("serviceName", "The booked service.", TemplateVariableKind.Text, "Annual vaccination", []),
                new NotificationTemplateVariableDefinition("startsOnLocal", "The cancelled appointment's start time, in the branch's local time.", TemplateVariableKind.Text, "Mon 12 Oct 2026, 10:30", []),
                new NotificationTemplateVariableDefinition("cancelledByClinic", "\"yes\" if the clinic cancelled it, \"no\" if the customer did.", TemplateVariableKind.Text, "no", []),
                new NotificationTemplateVariableDefinition("bookLink", "A link to book a new appointment.", TemplateVariableKind.Url, "https://portal.example.com/book", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s {{serviceName}} appointment on {{startsOnLocal}} has been cancelled. Book again: {{bookLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Your appointment has been cancelled", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s {{serviceName}} appointment on {{startsOnLocal}} has been cancelled. [Book a new appointment]({{bookLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Appointment cancelled",
                    "{{catName}}'s {{serviceName}} appointment on {{startsOnLocal}} has been cancelled.", "{{bookLink}}"),
            ]),

        new NotificationTemplateDefinition(
            WaitlistSlotOffered, "Waitlist slot offered", "Sent when a freed slot is offered to the next waitlisted customer (HO-APT-001 §8.11).",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("serviceName", "The waitlisted service.", TemplateVariableKind.Text, "Annual vaccination", []),
                new NotificationTemplateVariableDefinition("startsOnLocal", "The offered slot's start time, in the branch's local time.", TemplateVariableKind.Text, "Mon 12 Oct 2026, 10:30", []),
                new NotificationTemplateVariableDefinition("claimLink", "A link to claim the offered slot.", TemplateVariableKind.Url, "https://portal.example.com/portal/appointments/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
                new NotificationTemplateVariableDefinition("claimByLocal", "The claim window's deadline, in the branch's local time.", TemplateVariableKind.Text, "Mon 12 Oct 2026, 11:00", []),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "A slot for {{catName}}'s {{serviceName}} at {{startsOnLocal}} is available. Claim by {{claimByLocal}}: {{claimLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "A waitlisted slot is available", null,
                    "Hello {{recipientName}},\n\nA slot for {{catName}}'s {{serviceName}} at {{startsOnLocal}} is now available. [Claim it]({{claimLink}}) by {{claimByLocal}}.", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Waitlisted slot available",
                    "A slot for {{catName}}'s {{serviceName}} at {{startsOnLocal}} is available. Claim by {{claimByLocal}}.", "{{claimLink}}"),
            ]),

        new NotificationTemplateDefinition(
            ShopBackInStock, "Back in stock", "Sent once when an item a customer asked to be told about is available again (HO-SHP-001 §8.11). Marketing: sent only on a channel the customer has granted marketing consent for.",
            NotificationCategory.Marketing, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("productName", "The product's name.", TemplateVariableKind.Text, "Royal Canin Kitten", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
                new NotificationTemplateVariableDefinition("variantLabel", "The variant, such as \"2 kg · Salmon\".", TemplateVariableKind.Text, "2 kg", []),
                new NotificationTemplateVariableDefinition("productLink", "A link to the product page with the variant selected.", TemplateVariableKind.Url, "https://portal.example.com/shop/products/royal-canin-kitten?sku=00000000-0000-0000-0000-000000000000", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{productName}} ({{variantLabel}}) is back in stock: {{productLink}} Stop alerts: {{unsubscribeLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "{{productName}} is back in stock", null,
                    "Hello {{recipientName}},\n\n{{productName}} ({{variantLabel}}) is back in stock. [See it in the shop]({{productLink}}).\n\n[Stop these alerts]({{unsubscribeLink}})", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Back in stock",
                    "{{productName}} ({{variantLabel}}) is back in stock. Stop alerts: {{unsubscribeLink}}", "{{productLink}}"),
            ]),

        // HO-EMR-001 §8.8 -- on finalisation, to the cat's current owner. Link-only: no diagnosis, no
        // vet name, no clinical text. The portal link is where access is authenticated and logged.
        new NotificationTemplateDefinition(
            VisitSummaryAvailable, "Visit summary available", "Sent when a vet finalises a visit, so the owner can read the discharge summary in the portal (HO-EMR-001 §8.8). Link only -- no clinical content.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("visitDate", "The visit's date.", TemplateVariableKind.Date, "2026-10-12", []),
                new NotificationTemplateVariableDefinition("summaryLink", "A link to the visit summary in the portal.", TemplateVariableKind.Url, "https://portal.example.com/portal/visits/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s visit summary from {{visitDate}} is ready: {{summaryLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "{{catName}}'s visit summary is ready", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s visit summary from {{visitDate}} is ready. [Read the summary]({{summaryLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Visit summary ready",
                    "{{catName}}'s visit summary from {{visitDate}} is ready.", "{{summaryLink}}"),
            ]),

        // HO-EMR-001 §8.9 -- a follow-up reminder queued with SendAfter and the VisitFollowUp guard. Never
        // states the reason for the follow-up.
        new NotificationTemplateDefinition(
            VisitFollowUpDue, "Follow-up visit due", "Sent ahead of a cat's follow-up visit date when the owner has not yet booked (HO-EMR-001 §8.9). No reason is stated.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("dueOn", "The follow-up's due date.", TemplateVariableKind.Date, "2026-10-20", []),
                new NotificationTemplateVariableDefinition("bookingLink", "A direct booking link.", TemplateVariableKind.Url, "https://portal.example.com/book?catId=sample&category=FollowUp", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}} is due for a follow-up visit on {{dueOn}}. Book here: {{bookingLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "A follow-up visit is due for {{catName}}", null,
                    "Hello {{recipientName}},\n\n{{catName}} is due for a follow-up visit on {{dueOn}}. [Book now]({{bookingLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Follow-up visit due",
                    "{{catName}} is due for a follow-up visit on {{dueOn}}.", "{{bookingLink}}"),
            ]),

        new NotificationTemplateDefinition(
            InventoryBatchRecall, "Product recall", "Sent once to every customer who received a recalled batch, or whose cat did (HO-IMP-001 §8.12). A safety notice: transactional, urgent, ignores quiet hours (OQ-6).",
            NotificationCategory.Transactional, NotificationUrgency.Urgent,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("productName", "The recalled product's name.", TemplateVariableKind.Text, "Royal Canin Kitten 2 kg", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
                new NotificationTemplateVariableDefinition("lotNumber", "The recalled lot.", TemplateVariableKind.Text, "L2406-17", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
                // Text rather than Date (a deviation from §8.12, recorded in HO-IMP-001 §1 Amended): an undated
                // batch (a General SKU) has no expiry to give, and a Date variable that is not a date cannot render.
                new NotificationTemplateVariableDefinition("expiresOn", "The lot's expiry date (yyyy-MM-dd), or \"no expiry date\".", TemplateVariableKind.Text, "2027-03-31", []),
                new NotificationTemplateVariableDefinition("customerAdvice", "What the owner should do.", TemplateVariableKind.Text, "Stop feeding it and keep the packaging.", [NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "Safety recall from {{clinicName}}: {{productName}}, lot {{lotNumber}} (expires {{expiresOn}}). Please stop using it and call {{clinicPhone}}.", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Safety recall: {{productName}}", null,
                    "Hello {{recipientName}},\n\n{{productName}}, lot {{lotNumber}} (expires {{expiresOn}}), has been recalled.\n\n{{customerAdvice}}\n\nPlease call {{clinicName}} on {{clinicPhone}} if you have any questions.", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Product recall",
                    "{{productName}}, lot {{lotNumber}} (expires {{expiresOn}}), has been recalled. {{customerAdvice}} Call {{clinicPhone}} with any questions.", null),
            ]),

        new NotificationTemplateDefinition(
            BillingInvoiceIssued, "Invoice issued", "Sent when an invoice is issued to a customer (HO-BIL-001 §8.12).",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("invoiceNumber", "The invoice number.", TemplateVariableKind.Text, "INV-MAIN-2627-000123", []),
                new NotificationTemplateVariableDefinition("totalText", "The invoice total, formatted with its currency.", TemplateVariableKind.Text, "BDT 1,500.00", []),
                new NotificationTemplateVariableDefinition("dueOn", "The date payment is due.", TemplateVariableKind.Date, "2026-10-12", []),
                new NotificationTemplateVariableDefinition("invoiceLink", "A link to view the invoice.", TemplateVariableKind.Url, "https://portal.example.com/portal/billing/invoices/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "Invoice {{invoiceNumber}} for {{totalText}} is due on {{dueOn}}. View: {{invoiceLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Your invoice {{invoiceNumber}}", null,
                    "Hello {{recipientName}},\n\nInvoice {{invoiceNumber}} for {{totalText}} is due on {{dueOn}}. [View your invoice]({{invoiceLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "New invoice",
                    "Invoice {{invoiceNumber}} for {{totalText}} is due on {{dueOn}}.", "{{invoiceLink}}"),
            ]),

        new NotificationTemplateDefinition(
            BillingPaymentReceived, "Payment received", "Sent when a payment is recorded and accepted (HO-BIL-001 §8.12).",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("amountText", "The amount received, formatted with its currency.", TemplateVariableKind.Text, "BDT 500.00", []),
                new NotificationTemplateVariableDefinition("methodName", "How the payment was made.", TemplateVariableKind.Text, "CashInStore", []),
                new NotificationTemplateVariableDefinition("receiptNumber", "The receipt number.", TemplateVariableKind.Text, "R-000123", []),
                new NotificationTemplateVariableDefinition("purposeText", "What the payment went towards.", TemplateVariableKind.Text, "INV-MAIN-2627-000123", []),
                new NotificationTemplateVariableDefinition("receiptLink", "A link to view the receipt.", TemplateVariableKind.Url, "https://portal.example.com/portal/billing/payments/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "We received {{amountText}} ({{methodName}}) for {{purposeText}}. Receipt {{receiptNumber}}: {{receiptLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Payment received - receipt {{receiptNumber}}", null,
                    "Hello {{recipientName}},\n\nWe received {{amountText}} ({{methodName}}) for {{purposeText}}. [View receipt {{receiptNumber}}]({{receiptLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Payment received",
                    "We received {{amountText}} ({{methodName}}) for {{purposeText}}. Receipt {{receiptNumber}}.", "{{receiptLink}}"),
            ]),

        new NotificationTemplateDefinition(
            BillingRefundIssued, "Refund issued", "Sent when a refund is issued to a customer (HO-BIL-001 §8.12). The reason is never sent.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("amountText", "The amount refunded, formatted with its currency.", TemplateVariableKind.Text, "BDT 200.00", []),
                new NotificationTemplateVariableDefinition("methodName", "How the refund was paid.", TemplateVariableKind.Text, "CashInStore", []),
                new NotificationTemplateVariableDefinition("creditNoteNumber", "The credit note number, or a dash.", TemplateVariableKind.Text, "CN-MAIN-2627-000007", []),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "We refunded {{amountText}} ({{methodName}}). Credit note: {{creditNoteNumber}}.", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "Your refund", null,
                    "Hello {{recipientName}},\n\nWe refunded {{amountText}} ({{methodName}}). Credit note: {{creditNoteNumber}}.", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Refund issued",
                    "We refunded {{amountText}} ({{methodName}}). Credit note: {{creditNoteNumber}}.", null),
            ]),

        .. OrderDefinitions(),
        .. SurgeryDefinitions(),
        .. BoardingDefinitions(),
        .. ReportingDefinitions(),
        new NotificationTemplateDefinition(
            PharmacyRefillDue, "Prescription refill due", "Sent ahead of the date a repeat prescription is due, with a direct link to request it (HO-PHM-001 §8.11, US-NTF-03). No dose is stated.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("medicineName", "The medicine's name.", TemplateVariableKind.Text, "Meloxicam oral suspension", []),
                new NotificationTemplateVariableDefinition("refillDueOn", "The date the next supply is due.", TemplateVariableKind.Date, "2026-10-12", []),
                new NotificationTemplateVariableDefinition("validUntil", "The last day the repeat can be supplied.", TemplateVariableKind.Date, "2026-12-31", []),
                new NotificationTemplateVariableDefinition("repeatLink", "A direct link to request the repeat.", TemplateVariableKind.Url, "https://portal.example.com/portal/prescriptions/sample?action=repeat", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s {{medicineName}} refill is due on {{refillDueOn}}. Request it here: {{repeatLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "A refill is coming up for {{catName}}", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s {{medicineName}} refill is due on {{refillDueOn}}, and the prescription can be repeated until {{validUntil}}. [Request the repeat]({{repeatLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Refill due soon",
                    "{{catName}}'s {{medicineName}} refill is due on {{refillDueOn}}.", "{{repeatLink}}"),
            ]),

        new NotificationTemplateDefinition(
            PharmacyRepeatReady, "Repeat ready to collect", "Sent when a requested repeat has been dispensed and is ready (HO-PHM-001 §8.11).",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("medicineName", "The medicine's name.", TemplateVariableKind.Text, "Meloxicam oral suspension", []),
                new NotificationTemplateVariableDefinition("branchName", "The branch to collect from.", TemplateVariableKind.Text, "Main branch", []),
                new NotificationTemplateVariableDefinition("prescriptionLink", "A link to the prescription.", TemplateVariableKind.Url, "https://portal.example.com/portal/prescriptions/sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s {{medicineName}} is ready to collect at {{branchName}}. Details: {{prescriptionLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "{{catName}}'s repeat is ready", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s {{medicineName}} is ready to collect at {{branchName}}. [View the prescription]({{prescriptionLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Repeat ready",
                    "{{catName}}'s {{medicineName}} is ready to collect at {{branchName}}.", "{{prescriptionLink}}"),
            ]),

        new NotificationTemplateDefinition(
            PharmacyRepeatDeclined, "Repeat request declined", "Sent when a vet declines a repeat request (HO-PHM-001 §8.11). The reason is the vet's plain-text note, inserted as literal text.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("medicineName", "The medicine's name.", TemplateVariableKind.Text, "Meloxicam oral suspension", []),
                new NotificationTemplateVariableDefinition("declineReason", "Why the repeat was declined.", TemplateVariableKind.Text, "Please book a recheck first.", []),
                new NotificationTemplateVariableDefinition("bookLink", "A link to book a consultation.", TemplateVariableKind.Url, "https://portal.example.com/book?catId=sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "We could not repeat {{catName}}'s {{medicineName}}: {{declineReason}} Book here: {{bookLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "About {{catName}}'s repeat request", null,
                    "Hello {{recipientName}},\n\nWe could not repeat {{catName}}'s {{medicineName}}. {{declineReason}} [Book a consultation]({{bookLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Repeat request declined",
                    "We could not repeat {{catName}}'s {{medicineName}}. {{declineReason}}", "{{bookLink}}"),
            ]),

        new NotificationTemplateDefinition(
            PharmacyDietAuthorised, "Therapeutic diet authorised", "Sent when a vet authorises a prescription diet for a cat (HO-PHM-001 §8.11). No diagnosis is stated.",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("productName", "The authorised food.", TemplateVariableKind.Text, "Renal diet", []),
                new NotificationTemplateVariableDefinition("validTo", "The last day of the authorisation.", TemplateVariableKind.Date, "2027-01-31", []),
                new NotificationTemplateVariableDefinition("authorisationsLink", "A link to the cat's authorisations.", TemplateVariableKind.Url, "https://portal.example.com/portal/prescriptions?tab=diets", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}} can now be given {{productName}} until {{validTo}}. Details: {{authorisationsLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "{{productName}} is authorised for {{catName}}", null,
                    "Hello {{recipientName}},\n\nYour vet has authorised {{productName}} for {{catName}} until {{validTo}}. [View authorisations]({{authorisationsLink}}).", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Diet authorised",
                    "{{productName}} is authorised for {{catName}} until {{validTo}}.", "{{authorisationsLink}}"),
            ]),

        new NotificationTemplateDefinition(
            PharmacyDietAuthorisationExpired, "Therapeutic diet authorisation ended", "Sent when a diet authorisation has ended and the food is locked again (HO-PHM-001 §8.11).",
            NotificationCategory.Transactional, NotificationUrgency.Normal,
            [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("catName", "The cat's name.", TemplateVariableKind.Text, "Mitu", []),
                new NotificationTemplateVariableDefinition("productName", "The food that was authorised.", TemplateVariableKind.Text, "Renal diet", []),
                new NotificationTemplateVariableDefinition("expiredOn", "The date the authorisation ended.", TemplateVariableKind.Date, "2027-01-31", []),
                new NotificationTemplateVariableDefinition("bookLink", "A link to book a consultation.", TemplateVariableKind.Url, "https://portal.example.com/book?catId=sample", [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null,
                    "{{catName}}'s authorisation for {{productName}} ended on {{expiredOn}}. Book a consultation to renew it: {{bookLink}}", null),
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "{{catName}}'s diet authorisation has ended", null,
                    "Hello {{recipientName}},\n\n{{catName}}'s authorisation for {{productName}} ended on {{expiredOn}}. [Book a consultation]({{bookLink}}) to renew it.", null),
                new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Diet authorisation ended",
                    "{{catName}}'s authorisation for {{productName}} ended on {{expiredOn}}.", "{{bookLink}}"),
            ]),
    ];

    public static NotificationTemplateDefinition? Find(string key) =>
        Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));
}
