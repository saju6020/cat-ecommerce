using System.Globalization;

namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-BIL-001 §12.4 money JSON: <c>{ "amount": "1500.00", "currencyCode": "BDT" }</c> (amount a string).</summary>
public static class BillingMoney
{
    public static MoneyViewModel Of(decimal amount, string currencyCode) =>
        new(amount.ToString("F2", CultureInfo.InvariantCulture), currencyCode);

    public static string Quantity(decimal quantity) => quantity.ToString("F3", CultureInfo.InvariantCulture);
}

// ── Configuration and reference (§12.4) ─────────────────────────────────────────────────────────────
public sealed record PaymentMethodViewModel(
    int Value, string Name, bool CountsInTill, bool RequiresReference, string ReferenceLabel, bool RequiresCollector,
    bool IsOnline, IReadOnlyList<string> AllowedRefundMethods);

public sealed record BillingBankAccountViewModel(
    string BankName, string AccountName, string AccountNumber, string? BranchName, string? RoutingNumber);

public sealed record BillingProfileViewModel(
    Guid BranchId, string LegalName, string TaxRegistrationNumber, IReadOnlyList<string> AddressLines, string? Phone,
    string? Email, string? InvoiceFooter, IReadOnlyList<BillingBankAccountViewModel> BankAccounts, int Version);

// ── Charges (§12.4) ─────────────────────────────────────────────────────────────────────────────────
public sealed record BilledOnInvoiceViewModel(Guid InvoiceId, string? Number, string Status);

public sealed record ChargeViewModel(
    Guid ChargeId, Guid CustomerId, string CustomerName, Guid BranchId, Guid? CatId, string BusinessLine, string ItemKind,
    string SourceKind, Guid SourceId, string SourceLineRef, string? BillingGroupKind, Guid? BillingGroupId, string Description,
    string Quantity, MoneyViewModel UnitPrice, MoneyViewModel LineDiscount, MoneyViewModel LineTotal, Guid TaxClassId, DateOnly ServiceDate,
    string Status, DateTimeOffset RecordedOn, string? VoidReason, BilledOnInvoiceViewModel? BilledOnInvoice);

public sealed record UncapturedSourceViewModel(string SourceKind, Guid SourceId, Guid CustomerId, DateTimeOffset CompletedOn, string Label);

// ── Invoices (§12.4) ────────────────────────────────────────────────────────────────────────────────
public sealed record InvoiceLineViewModel(
    Guid LineId, Guid? ChargeId, string BusinessLine, string ItemKind, string SourceKind, Guid SourceId, Guid? CatId, string Description,
    string Quantity, MoneyViewModel UnitPrice, MoneyViewModel LineDiscount, MoneyViewModel InvoiceDiscountShare, Guid TaxClassId,
    string? TaxClassCode, string TaxRatePercent, MoneyViewModel NetAmount, MoneyViewModel TaxAmount, MoneyViewModel GrossAmount, int SortOrder);

public sealed record SellerViewModel(
    string LegalName, string TaxRegistrationNumber, string AddressText, string? Phone, string? Email, string? InvoiceFooter,
    IReadOnlyList<BillingBankAccountViewModel> BankAccounts);

public sealed record BillToViewModel(string FullName, string? Mobile, string? Email, string? AddressText);

public sealed record LoyaltyRedemptionViewModel(int Points, MoneyViewModel Value, Guid LoyaltyEntryId);

public sealed record InvoiceTotalsViewModel(MoneyViewModel Net, MoneyViewModel Discount, MoneyViewModel Tax, MoneyViewModel Total);

public sealed record InvoiceSummaryViewModel(
    Guid InvoiceId, string? Number, string Status, Guid CustomerId, string CustomerName, Guid BranchId, DateOnly? IssueDate, DateOnly? DueOn,
    MoneyViewModel Total, MoneyViewModel Paid, MoneyViewModel Credited, MoneyViewModel Outstanding, int Version);

public sealed record InvoicePaymentViewModel(
    Guid PaymentId, string ReceiptNumber, string Method, MoneyViewModel Amount, DateTimeOffset On, string Status);

public sealed record InvoiceCreditNoteViewModel(Guid CreditNoteId, string Number, string Kind, MoneyViewModel Total, DateOnly IssueDate);

public sealed record InvoiceDetailViewModel(
    Guid InvoiceId, string? Number, string? FinancialYear, string Status, int Version, Guid BranchId, string BranchTimeZoneId, Guid CustomerId,
    DateTimeOffset? IssuedOn, DateOnly? IssueDate, DateOnly? DueOn, SellerViewModel? Seller, BillToViewModel? BillTo,
    IReadOnlyList<InvoiceLineViewModel> Lines, InvoiceTotalsViewModel Totals, bool IsEstimate,
    MoneyViewModel Paid, MoneyViewModel Credited, MoneyViewModel Outstanding, LoyaltyRedemptionViewModel? LoyaltyRedemption, string? Notes,
    Guid? CorrectsInvoiceId, Guid? CorrectsCreditNoteId, Guid? CorrectedByInvoiceId, IReadOnlyList<InvoicePaymentViewModel> Payments,
    IReadOnlyList<InvoiceCreditNoteViewModel> CreditNotes, IReadOnlyList<string> AllowedActions);

public sealed record InvoicePreviewLineViewModel(
    Guid ChargeId, string BusinessLine, string ItemKind, string Description, string Quantity, MoneyViewModel UnitPrice, MoneyViewModel LineDiscount,
    MoneyViewModel InvoiceDiscountShare, string? TaxClassCode, string? TaxRatePercent, MoneyViewModel NetAmount, MoneyViewModel TaxAmount, MoneyViewModel GrossAmount);

/// <summary><c>isEstimate: true</c> always -- a preview is computed at <b>today's</b> rates and writes nothing (§12.4).</summary>
public sealed record InvoicePreviewViewModel(
    Guid CustomerId, IReadOnlyList<InvoicePreviewLineViewModel> Lines, InvoiceTotalsViewModel Totals, bool IsEstimate, IReadOnlyList<string> Problems);

/// <summary>The customer's "how to pay" (§12.4): the issuing branch's bank accounts and the reference to use.</summary>
public sealed record HowToPayViewModel(IReadOnlyList<BillingBankAccountViewModel> BankAccounts, string ReferenceInstruction);

public sealed record CustomerInvoiceDetailViewModel(InvoiceDetailViewModel Invoice, HowToPayViewModel HowToPay);
