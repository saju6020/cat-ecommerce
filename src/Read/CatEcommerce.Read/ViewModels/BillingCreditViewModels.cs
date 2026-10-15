namespace CatEcommerce.Read.ViewModels;

// Credit notes and refunds (HO-BIL-001 section 12.4)
public sealed record CreditNoteLineViewModel(
    Guid InvoiceLineId, string Description, string? Quantity, MoneyViewModel? RequestedAmount, MoneyViewModel Net, string TaxRatePercent,
    MoneyViewModel Tax, MoneyViewModel Gross);

public sealed record CreditNoteDetailViewModel(
    Guid CreditNoteId, string Number, string FinancialYear, string Kind, string Reason, Guid InvoiceId, string? InvoiceNumber, Guid CustomerId, Guid BranchId,
    IReadOnlyList<CreditNoteLineViewModel> Lines, MoneyViewModel Net, MoneyViewModel Tax, MoneyViewModel Total, MoneyViewModel ReleasedToCustomerCredit,
    DateTimeOffset IssuedOn, DateOnly IssueDate, Guid IssuedBy, string IssuedByName, Guid? CorrectedByInvoiceId);

public sealed record RefundListItemViewModel(
    Guid RefundId, Guid PaymentId, string? ReceiptNumber, Guid? CreditNoteId, string? CreditNoteNumber, Guid CustomerId, string CustomerName, string Method,
    MoneyViewModel Amount, string? Reference, string Reason, DateOnly BusinessDate, DateTimeOffset IssuedOn, Guid IssuedBy, string IssuedByName);
