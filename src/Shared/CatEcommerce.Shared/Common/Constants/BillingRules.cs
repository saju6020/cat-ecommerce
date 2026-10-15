namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §10.3 -- the Billing bounds, in one place.</summary>
public static class BillingRules
{
    public const int MaxInvoiceLines = 500;
    public const int MaxNumberingAttempts = 5;
    public const int MaxBackdatedReceiptDays = 31;
    public const int MaxAllocationEntries = 200;
    public const int MaxRequestedAllocations = 20;
    public const int MaxStatementLines = 5000;
    public const int MaxStatementBytes = 1_048_576;
    public const int MaxExportRangeDays = 366;
    public const int MaxExportRows = 200_000;
    public const int MaxRemittanceLines = 1000;
    public const int MaxBankAccounts = 5;
    public const int MaxIdempotencyKeyLength = 100;
    public const int MaxReferenceLength = 100;
    public const int MaxNotesLength = 500;
    public const int MaxReasonLength = 1000;
    public const int MaxDescriptionLength = 300;
    public const int MaxCourierCodeLength = 30;
    public const int MaxCourierNameLength = 100;
    public const int MaxEarmarkLabelLength = 200;
    public const int MaxChargeIdsPerRequest = 500;
    public const int MaxCreditNoteLines = 500;
    public const decimal MaxAmount = 9_999_999_999.99m;

    /// <summary>The zone used for "today" when a read has no branch to ask (the portal summary) -- the same default HO-NTF-001 falls
    /// back to; a branch-scoped read always uses its own branch zone (ADR-045).</summary>
    public const string DefaultTimeZoneId = "Asia/Dhaka";
}
