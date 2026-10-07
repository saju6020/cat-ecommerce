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
public static class NotificationTemplateCatalogue
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
    ];

    public static NotificationTemplateDefinition? Find(string key) =>
        Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));
}
