using System.Globalization;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-BIL-001 §10.3 -- every message that more than one path can produce (the aggregate rule, the
/// handler pre-check, the unique-index race in the filter), so the sequential and concurrent paths
/// for the same rule cannot drift (the <c>ShopMessages</c> precedent). Amounts are always formatted in
/// the currency, never raw (§12.4, NFR-USA-4).
/// </summary>
public static class BillingMessages
{
    public const string ReferenceLooksLikeCardNumber =
        "This looks like a card number. Enter the terminal slip number instead — card numbers must never be recorded.";

    public const string ChargeAlreadyInvoiced = "This charge is already on an invoice.";
    public const string ChargeAlreadyRecorded = "This charge has already been recorded.";
    public const string BankLineAlreadyMatched = "This statement line is already matched to a payment.";
    public const string PaymentAlreadyMatched = "This payment is already matched to a statement line.";
    public const string StatementDuplicate = "This statement file has already been imported for this account.";
    public const string InvoiceNotDraft = "Only a draft invoice can be changed. Issued invoices are corrected with a credit note.";
    public const string IdempotencyKeyConflict = "This Idempotency-Key was already used for a different request.";
    public const string BillingProfileMissing = "This branch has no billing profile yet. An administrator must set one before invoices can be issued.";
    public const string ApprovalWaiverNotAllowed = "The second approval can only be waived for a cash-on-delivery collection or a cash or card payment taken at the counter.";
    public const string ApproverMustDiffer = "A payment must be approved by a different staff member than the one who recorded it.";
    public const string VarianceExplanationRequired = "The counted cash differs from the expected cash. Enter an explanation before closing.";
    public const string PendingApprovalsExist = "Payments recorded for this day are still waiting for approval. Approve or reject them before closing.";

    /// <summary><c>BDT 1,500.00</c> -- invariant culture, grouping, two decimals.</summary>
    public static string FormatMoney(decimal amount, string currencyCode) =>
        $"{currencyCode} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    /// <summary>A receipt number prints as <c>R-000123</c>; a payment awaiting approval has none printed yet.</summary>
    public static string ReceiptNumber(long number) => "R-" + number.ToString("D6", CultureInfo.InvariantCulture);

    public static string AllocationExceedsOutstanding(decimal available, string currencyCode, string invoiceNumber) =>
        $"Only {FormatMoney(available, currencyCode)} is outstanding on {invoiceNumber}.";

    public static string AllocationExceedsUnallocated(decimal available, string currencyCode) =>
        $"Only {FormatMoney(available, currencyCode)} of this payment is still unallocated.";

    public static string RefundExceedsAvailable(decimal available, string currencyCode) =>
        $"Only {FormatMoney(available, currencyCode)} of this payment is available to refund.";

    public static string TaxRateNotConfigured(string taxClassCode) =>
        $"The tax class {taxClassCode} has no rate configured. Set a rate before issuing.";

    public static string CreditExceedsLine(string description) =>
        $"The credit for \"{description}\" is more than what remains on that line.";
}
