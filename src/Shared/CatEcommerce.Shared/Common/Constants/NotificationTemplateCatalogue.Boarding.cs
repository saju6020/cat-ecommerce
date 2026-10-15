using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-BRD-001 section 8.19 (ADR-226/ADR-231, renumbered ADR-359/ADR-364) -- the eleven boarding templates appended to HO-NTF-001's catalogue. All
/// <b>Transactional</b> with SMS, email and in-app. Urgency is a property of the definition (F33), so a medium and a high incident are two templates.
/// <b>Urgent</b>: reserved, confirmed, cancelled, high incident, vet-approval request (the owner is acting at that moment); the rest are Normal. No
/// description, note or health text ever goes in a message -- the link opens it behind sign-in. Enqueued by command handlers only (HO-NTF-001 ADR-057).
/// </summary>
public static partial class NotificationTemplateCatalogue
{
    public const string BoardingReserved = "boarding.reserved";
    public const string BoardingConfirmed = "boarding.confirmed";
    public const string BoardingReservationLapsed = "boarding.reservation-lapsed";
    public const string BoardingCancelled = "boarding.cancelled";
    public const string BoardingCheckedIn = "boarding.checked-in";
    public const string BoardingDailyUpdate = "boarding.daily-update";
    public const string BoardingIncidentMedium = "boarding.incident-medium";
    public const string BoardingIncidentHigh = "boarding.incident-high";
    public const string BoardingVetApprovalRequested = "boarding.vet-approval-requested";
    public const string BoardingCheckedOut = "boarding.checked-out";
    public const string BoardingWaitlistAvailable = "boarding.waitlist-available";

    /// <summary>The boarding templates whose urgency is Urgent -- pinned by U-A8.</summary>
    public static readonly IReadOnlyList<string> UrgentBoardingTemplateKeys =
        [BoardingReserved, BoardingConfirmed, BoardingCancelled, BoardingIncidentHigh, BoardingVetApprovalRequested];

    // A property, not a static field: Definitions (main partial file) is built during static initialisation and calls BoardingDefinitions().
    private static NotificationChannel[] BoardingAllChannels => [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp];

    private static NotificationTemplateVariableDefinition BoardingText(string name, string description, string sample) =>
        new(name, description, TemplateVariableKind.Text, sample, []);

    private static NotificationTemplateVariableDefinition BoardingDate(string name, string description, string sample) =>
        new(name, description, TemplateVariableKind.Date, sample, []);

    private static NotificationTemplateVariableDefinition BoardingLink(string name, string description, string sample, bool smsToo = true) =>
        new(name, description, TemplateVariableKind.Url, sample, smsToo ? BoardingAllChannels : [NotificationChannel.Email, NotificationChannel.InApp]);

    private static NotificationTemplateDefinition BoardingTemplate(
        string key, string displayName, string description, NotificationUrgency urgency,
        IReadOnlyList<NotificationTemplateVariableDefinition> variables,
        string sms, string emailSubject, string emailBody, string inAppTitle, string inAppBody, string? inAppLink) =>
        new(key, displayName, description, NotificationCategory.Transactional, urgency, BoardingAllChannels, ContainsSecret: false, variables,
        [
            new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, sms, null),
            new NotificationTemplateChannelDefault(NotificationChannel.Email, emailSubject, null, emailBody, null),
            new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, inAppTitle, inAppBody, inAppLink),
        ]);

    private static IReadOnlyList<NotificationTemplateDefinition> BoardingDefinitions() =>
    [
        BoardingTemplate(
            BoardingReserved, "Boarding reserved", "Sent the moment a stay is reserved, with how and when to pay the deposit (HO-BRD-001 section 8.19).", NotificationUrgency.Urgent,
            [
                BoardingText("catNames", "The cats' names.", "Mitu and Lulu"),
                BoardingDate("arrivalOn", "The arrival date.", "2026-11-02"),
                BoardingDate("departureOn", "The departure date.", "2026-11-07"),
                BoardingText("depositAmount", "The deposit to pay, formatted with its currency.", "BDT 1,500.00"),
                BoardingText("payByLocal", "When the deposit must be paid by, in the branch's local time.", "Mon 2 Nov 2026, 14:00"),
                new NotificationTemplateVariableDefinition("paymentInstructionsLink", "A link to how to pay the deposit.", TemplateVariableKind.Url, "https://portal.example.com/pages/boarding-payment-instructions", [NotificationChannel.Sms, NotificationChannel.Email]),
                BoardingLink("stayLink", "A link to the stay.", "https://portal.example.com/portal/boarding/sample", smsToo: false),
            ],
            "{{catNames}} is reserved from {{arrivalOn}} to {{departureOn}}. Pay the {{depositAmount}} deposit by {{payByLocal}} to keep the booking: {{paymentInstructionsLink}}",
            "Your boarding reservation: deposit due",
            "Hello {{recipientName}},\n\n{{catNames}} is reserved for boarding from {{arrivalOn}} to {{departureOn}}. To keep the booking please pay the {{depositAmount}} deposit by {{payByLocal}}. [How to pay]({{paymentInstructionsLink}}). [See your booking]({{stayLink}}).",
            "Boarding reserved",
            "{{catNames}} is reserved from {{arrivalOn}} to {{departureOn}}. Pay {{depositAmount}} by {{payByLocal}}.",
            "{{stayLink}}"),

        BoardingTemplate(
            BoardingConfirmed, "Boarding confirmed", "Sent when the deposit is received and the stay is confirmed (HO-BRD-001 section 8.19).", NotificationUrgency.Urgent,
            [
                BoardingText("catNames", "The cats' names.", "Mitu and Lulu"),
                BoardingDate("arrivalOn", "The arrival date.", "2026-11-02"),
                BoardingDate("departureOn", "The departure date.", "2026-11-07"),
                BoardingText("balanceDue", "The balance still to pay, formatted with its currency.", "BDT 3,500.00"),
                BoardingLink("stayLink", "A link to the stay.", "https://portal.example.com/portal/boarding/sample"),
            ],
            "{{catNames}}'s stay from {{arrivalOn}} to {{departureOn}} is confirmed. Balance due at check-out: {{balanceDue}}. Details: {{stayLink}}",
            "Your boarding is confirmed",
            "Hello {{recipientName}},\n\nWe have received your deposit and {{catNames}}'s stay from {{arrivalOn}} to {{departureOn}} is confirmed. The balance due is {{balanceDue}}. [See your booking]({{stayLink}}).",
            "Boarding confirmed",
            "{{catNames}}'s stay from {{arrivalOn}} to {{departureOn}} is confirmed. Balance due {{balanceDue}}.",
            "{{stayLink}}"),

        BoardingTemplate(
            BoardingReservationLapsed, "Boarding reservation lapsed", "Sent when a reservation is released because the deposit did not arrive in time (HO-BRD-001 section 8.19).", NotificationUrgency.Normal,
            [
                BoardingText("catNames", "The cats' names.", "Mitu and Lulu"),
                BoardingDate("arrivalOn", "The arrival date that was held.", "2026-11-02"),
                BoardingLink("bookLink", "A link to book again.", "https://portal.example.com/boarding/book"),
            ],
            "The reservation for {{catNames}} from {{arrivalOn}} was released because the deposit did not arrive in time. Book again: {{bookLink}}",
            "Your boarding reservation was released",
            "Hello {{recipientName}},\n\nThe reservation for {{catNames}} from {{arrivalOn}} was released because we did not receive the deposit in time. [Book again]({{bookLink}}).",
            "Reservation released",
            "The reservation for {{catNames}} from {{arrivalOn}} was released because the deposit did not arrive in time.",
            "{{bookLink}}"),

        BoardingTemplate(
            BoardingCancelled, "Boarding cancelled", "Sent when a stay is cancelled; the producer states what happens to the deposit (HO-BRD-001 section 8.19).", NotificationUrgency.Urgent,
            [
                BoardingText("catNames", "The cats' names.", "Mitu and Lulu"),
                BoardingDate("arrivalOn", "The arrival date that was cancelled.", "2026-11-02"),
                BoardingText("depositOutcome", "What happens to the deposit.", "will be refunded"),
                BoardingLink("stayLink", "A link to the stay.", "https://portal.example.com/portal/boarding/sample"),
            ],
            "The stay for {{catNames}} from {{arrivalOn}} is cancelled. Your deposit {{depositOutcome}}. Details: {{stayLink}}",
            "Your boarding stay is cancelled",
            "Hello {{recipientName}},\n\nThe boarding stay for {{catNames}} from {{arrivalOn}} has been cancelled. Your deposit {{depositOutcome}}. [See the booking]({{stayLink}}).",
            "Boarding cancelled",
            "The stay for {{catNames}} from {{arrivalOn}} is cancelled. Your deposit {{depositOutcome}}.",
            "{{stayLink}}"),

        BoardingTemplate(
            BoardingCheckedIn, "Boarding check-in", "Sent when the cats are checked in (HO-BRD-001 section 8.19).", NotificationUrgency.Normal,
            [
                BoardingText("catNames", "The cats' names.", "Mitu and Lulu"),
                BoardingDate("departureOn", "The departure date.", "2026-11-07"),
                BoardingLink("diaryLink", "A link to the stay diary.", "https://portal.example.com/portal/boarding/sample/diary"),
            ],
            "{{catNames}} checked in safely and will go home on {{departureOn}}. Follow the stay diary: {{diaryLink}}",
            "{{catNames}} has checked in",
            "Hello {{recipientName}},\n\n{{catNames}} has checked in and is settling in. The planned departure is {{departureOn}}. [Follow the stay diary]({{diaryLink}}).",
            "Checked in",
            "{{catNames}} has checked in. Planned departure {{departureOn}}.",
            "{{diaryLink}}"),

        BoardingTemplate(
            BoardingDailyUpdate, "Boarding daily update", "Sent once a day while the cats are in (HO-BRD-001 section 8.19). Guarded by BoardingStayInHouse.", NotificationUrgency.Normal,
            [
                BoardingText("catNames", "The cats' names.", "Mitu and Lulu"),
                BoardingDate("date", "The day of the update.", "2026-11-03"),
                BoardingLink("diaryLink", "A link to the stay diary.", "https://portal.example.com/portal/boarding/sample/diary"),
            ],
            "Today's update for {{catNames}} ({{date}}) is in the stay diary: {{diaryLink}}",
            "Today's update for {{catNames}}",
            "Hello {{recipientName}},\n\nThe stay diary for {{catNames}} has been updated for {{date}}. [Open the diary]({{diaryLink}}).",
            "Daily update",
            "The stay diary for {{catNames}} is updated for {{date}}.",
            "{{diaryLink}}"),

        BoardingTemplate(
            BoardingIncidentMedium, "Boarding incident (medium)", "Sent for a medium-severity incident (HO-BRD-001 section 8.19). No description is sent.", NotificationUrgency.Normal,
            BoardingIncidentVariables(),
            "An incident was recorded for {{catName}} ({{incidentKind}}, {{occurredOnLocal}}). Details: {{incidentLink}}",
            "An incident involving {{catName}}",
            "Hello {{recipientName}},\n\nAn incident ({{incidentKind}}) was recorded for {{catName}} at {{occurredOnLocal}}. [See the details]({{incidentLink}}).",
            "Incident recorded",
            "An incident ({{incidentKind}}) was recorded for {{catName}} at {{occurredOnLocal}}.",
            "{{incidentLink}}"),

        BoardingTemplate(
            BoardingIncidentHigh, "Boarding incident (high)", "Sent for a high-severity incident; Urgent, so it bypasses quiet hours (HO-BRD-001 section 8.19, FR-NTF-7). No description is sent.", NotificationUrgency.Urgent,
            BoardingIncidentVariables(),
            "Important: an incident was recorded for {{catName}} ({{incidentKind}}, {{occurredOnLocal}}). Please read and call us: {{incidentLink}}",
            "Important: an incident involving {{catName}}",
            "Hello {{recipientName}},\n\nAn important incident ({{incidentKind}}) was recorded for {{catName}} at {{occurredOnLocal}}. Please [read the details]({{incidentLink}}) and contact us.",
            "Important incident",
            "An important incident ({{incidentKind}}) was recorded for {{catName}} at {{occurredOnLocal}}. Please read and call us.",
            "{{incidentLink}}"),

        BoardingTemplate(
            BoardingVetApprovalRequested, "Boarding vet charge approval", "Sent when a vet charge above the cat's spending limit needs the owner's approval (HO-BRD-001 section 8.19). The description is not sent.", NotificationUrgency.Urgent,
            [
                BoardingText("catName", "The cat's name.", "Mitu"),
                BoardingText("amount", "The charge to approve, formatted with its currency.", "BDT 4,500.00"),
                BoardingLink("approvalLink", "A link to approve or decline.", "https://portal.example.com/portal/boarding/sample/approvals/sample"),
            ],
            "We need your approval for a vet charge of {{amount}} for {{catName}}. Please answer here: {{approvalLink}}",
            "Approval needed for {{catName}}'s treatment",
            "Hello {{recipientName}},\n\nA vet charge of {{amount}} for {{catName}} is above the spending limit you set. [Approve or decline it]({{approvalLink}}).",
            "Approval needed",
            "A vet charge of {{amount}} for {{catName}} needs your approval.",
            "{{approvalLink}}"),

        BoardingTemplate(
            BoardingCheckedOut, "Boarding check-out", "Sent when the cats are checked out, with the stay summary (HO-BRD-001 section 8.19).", NotificationUrgency.Normal,
            [
                BoardingText("catNames", "The cats' names.", "Mitu and Lulu"),
                BoardingLink("summaryLink", "A link to the stay summary.", "https://portal.example.com/portal/boarding/sample/summary"),
            ],
            "{{catNames}} has checked out. Read the stay summary: {{summaryLink}}",
            "{{catNames}} has gone home",
            "Hello {{recipientName}},\n\n{{catNames}} has checked out. [Read the stay summary]({{summaryLink}}).",
            "Checked out",
            "{{catNames}} has checked out. The stay summary is ready.",
            "{{summaryLink}}"),

        BoardingTemplate(
            BoardingWaitlistAvailable, "Boarding waitlist: dates available", "Sent to the oldest waitlist entry when a unit frees (HO-BRD-001 section 8.19). Nothing is held (ADR-235 as renumbered ADR-368).", NotificationUrgency.Normal,
            [
                BoardingDate("arrivalOn", "The arrival date asked for.", "2026-11-02"),
                BoardingDate("departureOn", "The departure date asked for.", "2026-11-07"),
                BoardingLink("bookLink", "A link to book.", "https://portal.example.com/boarding/book"),
            ],
            "A unit is now available from {{arrivalOn}} to {{departureOn}}. It is not held for you: book now: {{bookLink}}",
            "Boarding is now available for your dates",
            "Hello {{recipientName}},\n\nA unit is now available from {{arrivalOn}} to {{departureOn}}. It is not held for you, so please [book now]({{bookLink}}).",
            "Boarding available",
            "A unit is now available from {{arrivalOn}} to {{departureOn}}. It is not held for you.",
            "{{bookLink}}"),
    ];

    private static IReadOnlyList<NotificationTemplateVariableDefinition> BoardingIncidentVariables() =>
    [
        BoardingText("catName", "The cat's name.", "Mitu"),
        BoardingText("incidentKind", "The kind of incident.", "Injury"),
        BoardingText("occurredOnLocal", "When it happened, in the branch's local time.", "Tue 3 Nov 2026, 22:30"),
        BoardingLink("incidentLink", "A link to the incident.", "https://portal.example.com/portal/boarding/sample/incidents/sample"),
    ];
}
