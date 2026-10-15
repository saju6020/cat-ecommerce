namespace CatEcommerce.Read.ViewModels;

public sealed record AccountingExportRunViewModel(
    Guid RunId, DateOnly From, DateOnly To, Guid? BranchId, int InvoiceCount, int InvoiceLineCount, int PaymentCount, int AllocationCount, int CreditNoteCount,
    int RefundCount, string Sha256, Guid RunBy, string RunByName, DateTimeOffset RunOn);
