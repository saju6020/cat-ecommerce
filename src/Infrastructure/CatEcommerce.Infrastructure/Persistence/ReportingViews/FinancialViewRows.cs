namespace CatEcommerce.Infrastructure.Persistence.ReportingViews;

// HO-RPT-001 section 8.4 -- the keyless row types of the phase D (revenue and payments) views. Money is decimal everywhere; no float or double (U-D4, C-6).

public sealed class RvInvoiceLineRow
{
    public Guid InvoiceLineId { get; set; }
    public Guid InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public int InvoiceStatus { get; set; }
    public DateTimeOffset? IssuedOn { get; set; }
    public DateOnly? IssueDate { get; set; }
    public int BusinessLine { get; set; }
    public int ItemKind { get; set; }
    public int SourceKind { get; set; }
    public Guid SourceId { get; set; }
    public Guid? SourceLineId { get; set; }
    public Guid? SkuId { get; set; }
    public Guid? ProductId { get; set; }
    public int? ServiceCategory { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal InvoiceDiscountShare { get; set; }
    public decimal NetAmount { get; set; }
    public Guid TaxClassId { get; set; }
    public string? TaxClassCode { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

public sealed class RvCreditNoteLineRow
{
    public Guid CreditNoteLineId { get; set; }
    public Guid CreditNoteId { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public Guid InvoiceLineId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset IssuedOn { get; set; }
    public int CreditNoteKind { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TaxRatePercent { get; set; }
    public string? TaxClassCode { get; set; }
    public int BusinessLine { get; set; }
    public int ItemKind { get; set; }
    public int SourceKind { get; set; }
    public Guid? SourceLineId { get; set; }
    public Guid? SkuId { get; set; }
    public Guid? ProductId { get; set; }
    public int? ServiceCategory { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
}

public sealed class RvCreditNoteRow
{
    public Guid CreditNoteId { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset IssuedOn { get; set; }
    public int CreditNoteKind { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal NetTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
    public decimal ReleasedToCustomerCredit { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

public sealed class RvRefundRow
{
    public Guid RefundId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid? CreditNoteId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public int Method { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset IssuedOn { get; set; }
    public DateOnly BusinessDate { get; set; }
}

public sealed class RvPaymentRow
{
    public Guid PaymentId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public int Method { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateOnly ReceivedOn { get; set; }
    public DateOnly? BusinessDate { get; set; }
    public int Status { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTimeOffset RecordedOn { get; set; }
    public DateTimeOffset? AcceptedOn { get; set; }
    public int? CollectorKind { get; set; }
    public string? CourierCode { get; set; }
    public decimal RemittedAmount { get; set; }
    public bool BankMatched { get; set; }
}

public sealed class RvCashUpRow
{
    public Guid CashUpId { get; set; }
    public Guid BranchId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public int Status { get; set; }
    public decimal? CashRecorded { get; set; }
    public decimal? CardRecorded { get; set; }
    public decimal? TransferRecorded { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Variance { get; set; }
    public string? Explanation { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTimeOffset? ClosedOn { get; set; }
}

public sealed class RvInvoiceBalanceRow
{
    public Guid InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset? IssuedOn { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? DueOn { get; set; }
    public int Status { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal CreditedTotal { get; set; }
    public decimal OutstandingAmount { get; set; }
}
