using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-SRG-001 §8.10 (ADR-169) -- the six surgery templates appended to HO-NTF-001's catalogue. All <b>Transactional</b> (medical, FR-NTF-4) with SMS, email and
/// in-app. <b>Urgent</b>: booked, rescheduled, cancelled and post-op instructions (the recipient is acting at that moment -- reception books with the owner on
/// the phone, the owner is collecting the cat now); the two timed reminders are Normal. SMS bodies carry the link, never the instructions (480-character limit
/// and medical content). Enqueued by command handlers only (HO-NTF-001 ADR-057); the timed ones ride <c>SendAfter</c> with the SurgeryPreOp / SurgeryFollowUp guards.
/// </summary>
public static partial class NotificationTemplateCatalogue
{
    public const string SurgeryBooked = "surgery.booked";
    public const string SurgeryPreOpReminder = "surgery.pre-op-reminder";
    public const string SurgeryRescheduled = "surgery.rescheduled";
    public const string SurgeryCancelled = "surgery.cancelled";
    public const string SurgeryPostOpInstructions = "surgery.post-op-instructions";
    public const string SurgeryFollowUpDue = "surgery.follow-up-due";

    /// <summary>The templates whose urgency is Urgent -- pinned by U-B11.</summary>
    public static readonly IReadOnlyList<string> UrgentSurgeryTemplateKeys = [SurgeryBooked, SurgeryRescheduled, SurgeryCancelled, SurgeryPostOpInstructions];

    // A property, not a static field: Definitions (main partial file) is built during static initialisation and calls SurgeryDefinitions().
    private static NotificationChannel[] SurgeryAllChannels => [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp];

    private static NotificationTemplateVariableDefinition SurgeryText(string name, string description, string sample, NotificationChannel[]? requiredOn = null) =>
        new(name, description, TemplateVariableKind.Text, sample, requiredOn ?? []);

    private static NotificationTemplateVariableDefinition SurgeryLink(string name, string description, string sample) =>
        new(name, description, TemplateVariableKind.Url, sample, SurgeryAllChannels);

    private static NotificationTemplateDefinition SurgeryTemplate(
        string key, string displayName, string description, NotificationUrgency urgency,
        IReadOnlyList<NotificationTemplateVariableDefinition> variables,
        string sms, string emailSubject, string emailBody, string inAppTitle, string inAppBody, string? inAppLink) =>
        new(key, displayName, description, NotificationCategory.Transactional, urgency, SurgeryAllChannels, ContainsSecret: false, variables,
        [
            new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, sms, null),
            new NotificationTemplateChannelDefault(NotificationChannel.Email, emailSubject, null, emailBody, null),
            new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, inAppTitle, inAppBody, inAppLink),
        ]);

    private static IReadOnlyList<NotificationTemplateVariableDefinition> SurgeryBookingVariables(params NotificationTemplateVariableDefinition[] extra) =>
    [
        SurgeryText("catName", "The cat's name.", "Mitu"),
        SurgeryText("procedureName", "The procedure.", "Spay"),
        SurgeryText("startsOnLocal", "The planned start, in the branch's local time.", "Mon 12 Oct 2026, 09:00"),
        SurgeryText("admitByLocal", "The time to bring the cat in by, in the branch's local time.", "Mon 12 Oct 2026, 08:00"),
        SurgeryText("branchName", "The branch's name.", "Gulshan Clinic"),
        SurgeryText("branchAddress", "The branch's address.", "House 12, Road 5, Gulshan"),
        SurgeryText("fastingInstructions", "What the owner must do before surgery (plain text).", "No food from midnight; water until 08:00.", [NotificationChannel.Email, NotificationChannel.InApp]),
        SurgeryLink("detailsLink", "A link to the surgery details and fasting instructions.", "https://portal.example.com/portal/surgeries/sample"),
        .. extra,
    ];

    private static IReadOnlyList<NotificationTemplateDefinition> SurgeryDefinitions() =>
    [
        SurgeryTemplate(
            SurgeryBooked, "Surgery booked", "Sent the moment a surgery is booked (HO-SRG-001 §8.10).", NotificationUrgency.Urgent,
            SurgeryBookingVariables(),
            "{{catName}}'s {{procedureName}} is booked for {{startsOnLocal}} at {{branchName}}. Please bring {{catName}} by {{admitByLocal}}. Fasting instructions: {{detailsLink}}",
            "{{catName}}'s surgery is booked",
            "Hello {{recipientName}},\n\n{{catName}}'s {{procedureName}} is booked for {{startsOnLocal}} at {{branchName}} ({{branchAddress}}). Please bring {{catName}} by {{admitByLocal}}.\n\nBefore surgery: {{fastingInstructions}}\n\n[See the details]({{detailsLink}}).",
            "Surgery booked",
            "{{catName}}'s {{procedureName}} is booked for {{startsOnLocal}}. Bring {{catName}} by {{admitByLocal}}. {{fastingInstructions}}",
            "{{detailsLink}}"),

        SurgeryTemplate(
            SurgeryPreOpReminder, "Surgery reminder", "Sent ahead of a booked surgery (HO-SRG-001 §8.10).", NotificationUrgency.Normal,
            SurgeryBookingVariables(),
            "Reminder: {{catName}}'s {{procedureName}} is on {{startsOnLocal}}. Please bring {{catName}} to {{branchName}} by {{admitByLocal}}. Fasting instructions: {{detailsLink}}",
            "Reminder: {{catName}}'s surgery",
            "Hello {{recipientName}},\n\nA reminder that {{catName}}'s {{procedureName}} is on {{startsOnLocal}} at {{branchName}} ({{branchAddress}}). Please bring {{catName}} by {{admitByLocal}}.\n\nBefore surgery: {{fastingInstructions}}\n\n[See the details]({{detailsLink}}).",
            "Surgery reminder",
            "{{catName}}'s {{procedureName}} is on {{startsOnLocal}}. Bring {{catName}} by {{admitByLocal}}. {{fastingInstructions}}",
            "{{detailsLink}}"),

        SurgeryTemplate(
            SurgeryRescheduled, "Surgery rescheduled", "Sent when a surgery is moved (HO-SRG-001 §8.10).", NotificationUrgency.Urgent,
            SurgeryBookingVariables(SurgeryText("previousStartsOnLocal", "The previous start, in the branch's local time.", "Sun 11 Oct 2026, 09:00")),
            "{{catName}}'s {{procedureName}} moved from {{previousStartsOnLocal}} to {{startsOnLocal}}. Please bring {{catName}} by {{admitByLocal}}. Details: {{detailsLink}}",
            "{{catName}}'s surgery has moved",
            "Hello {{recipientName}},\n\n{{catName}}'s {{procedureName}} has moved from {{previousStartsOnLocal}} to {{startsOnLocal}} at {{branchName}}. Please bring {{catName}} by {{admitByLocal}}.\n\nBefore surgery: {{fastingInstructions}}\n\n[See the details]({{detailsLink}}).",
            "Surgery rescheduled",
            "{{catName}}'s {{procedureName}} moved to {{startsOnLocal}} (was {{previousStartsOnLocal}}). Bring {{catName}} by {{admitByLocal}}. {{fastingInstructions}}",
            "{{detailsLink}}"),

        SurgeryTemplate(
            SurgeryCancelled, "Surgery cancelled", "Sent when a surgery is cancelled (HO-SRG-001 §8.10).", NotificationUrgency.Urgent,
            [
                SurgeryText("catName", "The cat's name.", "Mitu"),
                SurgeryText("procedureName", "The procedure.", "Spay"),
                SurgeryText("startsOnLocal", "The planned start that was cancelled, in the branch's local time.", "Mon 12 Oct 2026, 09:00"),
            ],
            "{{catName}}'s {{procedureName}} planned for {{startsOnLocal}} has been cancelled. Please call the clinic to rebook.",
            "{{catName}}'s surgery is cancelled",
            "Hello {{recipientName}},\n\n{{catName}}'s {{procedureName}} planned for {{startsOnLocal}} has been cancelled. Please contact the clinic to book another time.",
            "Surgery cancelled",
            "{{catName}}'s {{procedureName}} planned for {{startsOnLocal}} has been cancelled.",
            null),

        SurgeryTemplate(
            SurgeryPostOpInstructions, "Post-op instructions", "Sent at discharge (HO-SRG-001 §8.10). The SMS carries the link, never the instructions.", NotificationUrgency.Urgent,
            [
                SurgeryText("catName", "The cat's name.", "Mitu"),
                SurgeryText("procedureName", "The procedure.", "Spay"),
                SurgeryText("followUpSummary", "The follow-ups booked after surgery.", "Wound check 13 Oct; suture removal 22 Oct.", [NotificationChannel.Email, NotificationChannel.InApp]),
                SurgeryLink("detailsLink", "A link to the post-op instructions.", "https://portal.example.com/portal/surgeries/sample"),
            ],
            "{{catName}} is ready to go home after {{procedureName}}. Aftercare instructions: {{detailsLink}}",
            "{{catName}} is ready to go home",
            "Hello {{recipientName}},\n\n{{catName}} has recovered well from {{procedureName}} and is ready to go home. Follow-ups: {{followUpSummary}}\n\n[Read the aftercare instructions]({{detailsLink}}).",
            "Ready to go home",
            "{{catName}} is ready to go home after {{procedureName}}. Follow-ups: {{followUpSummary}}",
            "{{detailsLink}}"),

        SurgeryTemplate(
            SurgeryFollowUpDue, "Surgery follow-up due", "Sent ahead of a post-op follow-up (HO-SRG-001 §8.10).", NotificationUrgency.Normal,
            [
                SurgeryText("catName", "The cat's name.", "Mitu"),
                SurgeryText("followUpName", "The follow-up.", "Wound check"),
                new NotificationTemplateVariableDefinition("dueOn", "The date the follow-up is due.", TemplateVariableKind.Date, "2026-10-13", []),
                SurgeryLink("bookingLink", "A link to book the follow-up.", "https://portal.example.com/book?catId=sample&category=FollowUp"),
            ],
            "{{catName}}'s {{followUpName}} is due on {{dueOn}}. Book here: {{bookingLink}}",
            "{{catName}}'s {{followUpName}} is due",
            "Hello {{recipientName}},\n\n{{catName}}'s {{followUpName}} is due on {{dueOn}}. [Book the visit]({{bookingLink}}).",
            "Follow-up due",
            "{{catName}}'s {{followUpName}} is due on {{dueOn}}.",
            "{{bookingLink}}"),
    ];
}
