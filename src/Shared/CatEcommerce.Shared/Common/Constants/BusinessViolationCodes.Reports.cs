namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 10.4 -- Reporting violation codes. <c>BranchNotAssigned</c> and <c>ActionNotPermitted</c> are other areas' and are reused, never redeclared.</summary>
public static partial class BusinessViolationCodes
{
    public const string UnknownReport = nameof(UnknownReport);
    public const string ReportRangeRequired = nameof(ReportRangeRequired);
    public const string ReportRangeInverted = nameof(ReportRangeInverted);
    public const string ReportRangeTooLong = nameof(ReportRangeTooLong);
    public const string InvalidReportParameter = nameof(InvalidReportParameter);
    public const string UnknownReportParameter = nameof(UnknownReportParameter);
    public const string CrossBranchReportNotPermitted = nameof(CrossBranchReportNotPermitted);
    public const string ReportTooLargeForCsv = nameof(ReportTooLargeForCsv);
    public const string ReportTooLargeForPdf = nameof(ReportTooLargeForPdf);
    public const string ReportTimedOut = nameof(ReportTimedOut);
    public const string ReportFormatNotSupported = nameof(ReportFormatNotSupported);
    public const string ReportScheduleNotFound = nameof(ReportScheduleNotFound);
    public const string InvalidReportSchedule = nameof(InvalidReportSchedule);
    public const string InvalidReportSchedulePeriod = nameof(InvalidReportSchedulePeriod);
    public const string ReportRecipientRequired = nameof(ReportRecipientRequired);
    public const string ReportRecipientLimitExceeded = nameof(ReportRecipientLimitExceeded);
    public const string ReportRecipientNotPermitted = nameof(ReportRecipientNotPermitted);
    public const string ReportRecipientHasNoEmail = nameof(ReportRecipientHasNoEmail);
    public const string ReportRecipientNotAssignedToBranch = nameof(ReportRecipientNotAssignedToBranch);
    public const string ReportScheduleNotActive = nameof(ReportScheduleNotActive);
    public const string ReportScheduleCancelled = nameof(ReportScheduleCancelled);
    public const string ReportFileNotStored = nameof(ReportFileNotStored);
}
