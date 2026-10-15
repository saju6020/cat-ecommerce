namespace CatEcommerce.Infrastructure.Persistence.ReportingViews;

/// <summary>HO-RPT-001 ADR-291 -- one money event of a customer (an invoice or a credit note, per business line), the customer being the survivor of a merge. Credit notes carry a negative net amount.</summary>
public sealed class RvCustomerActivityRow
{
    public Guid CustomerId { get; set; }
    public Guid BranchId { get; set; }
    public int BusinessLine { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public decimal NetAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int Kind { get; set; }

    public const int KindInvoice = 1;
    public const int KindCreditNote = 2;
}
