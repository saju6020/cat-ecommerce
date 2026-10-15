namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 10.4 -- plain-language messages, each with the next step. Shared by the filter validator and the handlers so the two cannot disagree.</summary>
public static class ReportingMessages
{
    public const string UnknownReport = "This report does not exist.";
    public const string ReportRangeRequired = "Choose a date range: both a start date and an end date.";
    public const string ReportRangeInverted = "The end date is before the start date. Choose an end date on or after the start date.";
    public static string ReportRangeTooLong(int maxDays) => $"This report covers at most {maxDays} days. Choose a shorter date range.";
    public static string InvalidReportParameter(string name) => $"The value given for '{name}' is not allowed. Choose one of the listed values.";
    public static string UnknownReportParameter(string name) => $"This report has no filter named '{name}'.";
    public const string CrossBranchReportNotPermitted = "Only an administrator or an accountant can run a report across all branches. Choose one branch.";
    public const string BranchNotAssigned = "You are not assigned to that branch. Choose one of your branches.";
    public const string ReportTooLargeForCsv = "Too many rows for a CSV export. Narrow the filters or choose a shorter date range.";
    public const string ReportTooLargeForPdf = "Too many rows for a PDF -- export CSV or narrow the filters.";
    public const string ReportTimedOut = "This report took too long to run. Choose a shorter date range or a single branch.";
    public const string NotDeliveredYet = "This part of the report is not delivered yet.";
    public const string RowCeilingReached = "Only the first rows are shown. Narrow the filters or export a shorter range.";
    public const string ClinicVisitChargesNotDelivered = "Charges for consultation notes, dispensed medicines and surgeries are not on invoices yet (Billing phase J), so clinic revenue covers appointment services and manual charges only.";
    public const string ServicesHaveNoCost = "Services have no cost: clinic margin covers medicines and consumables only.";
    public const string ReportFormatNotSupported = "A schedule can send a CSV or a PDF.";
    public static string InvalidReportSchedulePeriod(string title) => $"That period does not suit '{title}'. Choose one of the periods offered for this report.";
    public const string ReportRecipientRequired = "Choose at least one recipient.";
    public static string ReportRecipientLimitExceeded(int max) => $"A schedule can have at most {max} recipients.";
    public const string ReportScheduleCancelled = "This schedule was cancelled and cannot be changed. Create a new schedule instead.";
    public const string ReportScheduleNotFound = "This schedule does not exist.";
    public const string ReportRecipientHasNoEmail = "This person has no email address, so they cannot receive a scheduled report. Choose someone else or add an email address to their account.";
    public const string ReportRecipientNotPermitted = "This person is not allowed to open this report, so they cannot be sent it. Choose someone who can.";
    public const string ReportRecipientNotPermittedAllBranches = "Only an administrator or an accountant can be sent an all-branches report. Choose a branch, or choose someone who can open every branch.";
    public const string ReportRecipientNotAssignedToBranch = "This person is not assigned to the schedule's branch. Choose someone who is.";
    public const string ReportFileNotStored = "The report file could not be stored, so it was not sent. Try again.";
    public const string AttachmentTooLargeNote = "The export was too large to attach -- open the report in PurrCare.";
    public const string ActionNotPermitted = "You are not permitted to perform this action.";
}
