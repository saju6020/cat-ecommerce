namespace CatEcommerce.Infrastructure.Billing.Documents;

public sealed record DocumentBankAccount(string BankName, string AccountName, string AccountNumber, string? BranchName, string? RoutingNumber);

/// <summary>One printed line: <c>NetAmount</c> is after every discount and before tax (US-BIL-07).</summary>
public sealed record DocumentLine(
    int No, string BusinessLine, string ItemKind, string Description, decimal Quantity, decimal UnitPrice, decimal LineDiscount,
    decimal InvoiceDiscountShare, string? TaxClassCode, decimal TaxRatePercent, decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

public sealed record DocumentPayment(string ReceiptNumber, string Method, decimal Amount, DateOnly OnDate);

/// <summary>
/// The data of an invoice PDF (HO-BIL-001 §12.2 / NFR-LEG-3): the <b>seller and bill-to snapshots</b> taken at issue,
/// every line with its frozen rate and its business line, the totals, the payments applied, the loyalty row and the
/// bank details. <c>IssuedOn</c> is printed in the <b>branch</b> zone. It lives in Infrastructure -- like
/// <c>CatMedicalHistoryDocument</c> -- because Read already references Infrastructure and the renderer must not
/// reference Read.
/// </summary>
public sealed record InvoiceDocument(
    Guid InvoiceId, string Number, string CurrencyCode, DateTimeOffset IssuedOn, string BranchTimeZoneId, DateOnly? DueOn,
    string SellerLegalName, string SellerTaxRegistrationNumber, string SellerAddress, string? SellerPhone, string? SellerEmail, string? SellerFooter,
    IReadOnlyList<DocumentBankAccount> BankAccounts, string BillToName, string? BillToMobile, string? BillToEmail, string? BillToAddress,
    IReadOnlyList<DocumentLine> Lines, decimal NetTotal, decimal DiscountTotal, decimal TaxTotal, decimal Total,
    decimal PaidTotal, decimal CreditedTotal, int? LoyaltyPoints, decimal? LoyaltyValue, IReadOnlyList<DocumentPayment> Payments,
    string? CorrectsInvoiceNumber, string? CorrectsCreditNoteNumber);

/// <summary>Behind an interface so the Api never references QuestPDF (the <c>ICatMedicalHistoryPdfRenderer</c> precedent).</summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(InvoiceDocument document);
}

/// <summary>A credit note's printed document -- its own series, the original invoice's number, and the credited lines at
/// the invoice line's <b>frozen</b> rate (ADR-186).</summary>
public sealed record CreditNoteDocument(
    Guid CreditNoteId, string Number, string Kind, string CurrencyCode, DateTimeOffset IssuedOn, string BranchTimeZoneId,
    string InvoiceNumber, string SellerLegalName, string SellerTaxRegistrationNumber, string SellerAddress, string? SellerPhone, string? SellerEmail,
    string? SellerFooter, string BillToName, string? BillToMobile, string? BillToEmail, string? BillToAddress, string Reason,
    IReadOnlyList<DocumentLine> Lines, decimal NetTotal, decimal TaxTotal, decimal Total, decimal ReleasedToCustomerCredit);

public interface ICreditNotePdfRenderer
{
    byte[] Render(CreditNoteDocument document);
}
