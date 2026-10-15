namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BIL-001 §8.9 -- one credited line. <see cref="TaxRatePercent"/> is the <b>invoice
/// line's frozen rate</b>, never today's (ADR-186, U-A8). Exactly one of <see cref="Quantity"/> or
/// <see cref="RequestedAmount"/> is set for a partial credit; both are null for a full credit of what
/// remained. Append-only (C-ALL-4).</summary>
public sealed class CreditNoteLine
{
    public Guid Id { get; private set; }
    public Guid InvoiceLineId { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal? Quantity { get; private set; }
    public decimal? RequestedAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public int SortOrder { get; private set; }

    private CreditNoteLine() { }

    public CreditNoteLine(
        Guid id, Guid invoiceLineId, string description, decimal? quantity, decimal? requestedAmount,
        decimal netAmount, decimal taxRatePercent, decimal taxAmount, decimal grossAmount, int sortOrder)
    {
        Id = id;
        InvoiceLineId = invoiceLineId;
        Description = description;
        Quantity = quantity;
        RequestedAmount = requestedAmount;
        NetAmount = netAmount;
        TaxRatePercent = taxRatePercent;
        TaxAmount = taxAmount;
        GrossAmount = grossAmount;
        SortOrder = sortOrder;
    }
}
