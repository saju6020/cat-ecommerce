namespace CatEcommerce.Infrastructure.Billing.Export;

// HO-BIL-001 ADR-190 -- the immutable facts an accounting export is made of. Nothing here is a current status or a running total:
// those change between runs, and two runs over the same range must be identical.

public sealed record ExportInvoice(
    Guid Id, string Number, string FinancialYear, Guid BranchId, Guid CustomerId, string CustomerName, DateOnly IssueDate, DateOnly? DueOn, string CurrencyCode,
    decimal NetTotal, decimal DiscountTotal, decimal TaxTotal, decimal Total, Guid? CorrectsInvoiceId);

public sealed record ExportInvoiceLine(
    Guid Id, Guid InvoiceId, string InvoiceNumber, int SortOrder, string BusinessLine, string ItemKind, string Description, decimal Quantity, decimal UnitPrice,
    decimal LineDiscount, decimal InvoiceDiscountShare, string? TaxClassCode, decimal TaxRatePercent, decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

/// <summary><c>RecordType</c> is <c>Payment</c> (dated by its business date) or <c>PaymentReversal</c> (dated by the reversal business date).</summary>
public sealed record ExportPayment(
    string RecordType, Guid Id, long? ReceiptNumber, Guid BranchId, Guid CustomerId, string Method, decimal Amount, string CurrencyCode, DateOnly Date,
    DateOnly ReceivedOn, string? Reference, string? CollectorKind);

public sealed record ExportAllocation(
    Guid Id, Guid PaymentId, Guid InvoiceId, string Kind, string? ReleaseReason, decimal Amount, DateOnly Date, DateTimeOffset OccurredOn);

public sealed record ExportCreditNote(
    Guid Id, string Number, Guid InvoiceId, string InvoiceNumber, Guid BranchId, Guid CustomerId, string Kind, string CurrencyCode, DateOnly IssueDate,
    decimal NetTotal, decimal TaxTotal, decimal Total, decimal ReleasedToCustomerCredit);

public sealed record ExportCreditNoteLine(
    Guid Id, Guid CreditNoteId, string CreditNoteNumber, int SortOrder, Guid InvoiceLineId, string Description, decimal TaxRatePercent,
    decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

public sealed record ExportRefund(
    Guid Id, Guid PaymentId, Guid? CreditNoteId, Guid BranchId, Guid CustomerId, string Method, decimal Amount, string CurrencyCode, DateOnly BusinessDate, string? Reference);

public sealed record AccountingExportContent(
    DateOnly From, DateOnly To, Guid? BranchId, string CurrencyCode,
    IReadOnlyList<ExportInvoice> Invoices, IReadOnlyList<ExportInvoiceLine> InvoiceLines, IReadOnlyList<ExportPayment> Payments, IReadOnlyList<ExportAllocation> Allocations,
    IReadOnlyList<ExportCreditNote> CreditNotes, IReadOnlyList<ExportCreditNoteLine> CreditNoteLines, IReadOnlyList<ExportRefund> Refunds)
{
    public int TotalRows => Invoices.Count + InvoiceLines.Count + Payments.Count + Allocations.Count + CreditNotes.Count + CreditNoteLines.Count + Refunds.Count;
}

/// <summary>The built export: the ZIP bytes and their fingerprint, with what went into it (for the run record).</summary>
public sealed record AccountingExportFile(
    byte[] Bytes, string Sha256, int InvoiceCount, int InvoiceLineCount, int PaymentCount, int AllocationCount, int CreditNoteCount, int RefundCount);
