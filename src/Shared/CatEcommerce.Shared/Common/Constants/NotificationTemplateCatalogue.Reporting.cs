using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-RPT-001 section 11.7 item 5 (US-RPT-06) -- the one reporting template appended to HO-NTF-001's catalogue. <b>Transactional</b>, <b>Normal</b> urgency (quiet hours apply -- the
/// schedule preview says when delivery will be held), <b>Email only</b>, not secret. The report file is an attachment by reference; <c>reportLink</c> is required so the email is useful
/// even when the export was too large to attach.
/// </summary>
public static partial class NotificationTemplateCatalogue
{
    public const string ReportScheduledDelivery = "report.scheduled-delivery";

    private static IReadOnlyList<NotificationTemplateDefinition> ReportingDefinitions() =>
    [
        new NotificationTemplateDefinition(
            ReportScheduledDelivery, "Scheduled report", "Sent to each recipient of a scheduled report, with the export attached (HO-RPT-001 section 11.7, US-RPT-06).",
            NotificationCategory.Transactional, NotificationUrgency.Normal, [NotificationChannel.Email], ContainsSecret: false,
            Variables:
            [
                new NotificationTemplateVariableDefinition("reportTitle", "The report's title.", TemplateVariableKind.Text, "Revenue by business line", []),
                new NotificationTemplateVariableDefinition("periodLabel", "The period the report covers.", TemplateVariableKind.Text, "1 Oct 2026 to 7 Oct 2026", []),
                new NotificationTemplateVariableDefinition("branchName", "The branch, or All branches.", TemplateVariableKind.Text, "Gulshan Clinic", []),
                new NotificationTemplateVariableDefinition("generatedOnLocal", "When the report was generated, in the branch's local time.", TemplateVariableKind.Text, "Thu 8 Oct 2026, 07:00", []),
                new NotificationTemplateVariableDefinition("rowCount", "The number of rows in the report.", TemplateVariableKind.Text, "42", []),
                new NotificationTemplateVariableDefinition("attachmentNote", "Empty, or why the file is not attached.", TemplateVariableKind.Text, "", []),
                new NotificationTemplateVariableDefinition("reportLink", "A link to the report in PurrCare.", TemplateVariableKind.Url, "https://staff.example.com/staff/reports/sample", [NotificationChannel.Email]),
            ],
            Defaults:
            [
                new NotificationTemplateChannelDefault(NotificationChannel.Email, "{{reportTitle}} -- {{periodLabel}}", null,
                    "Hello {{recipientName}},\n\nYour scheduled report \"{{reportTitle}}\" for {{periodLabel}} ({{branchName}}) was generated on {{generatedOnLocal}} and has {{rowCount}} rows.\n\n{{attachmentNote}}\n\n[Open the report in PurrCare]({{reportLink}})", null),
            ]),
    ];
}
