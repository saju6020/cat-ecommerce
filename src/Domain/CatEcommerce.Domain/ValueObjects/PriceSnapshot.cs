namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-PHM-001 §8.7 / ADR-154 -- the SKU price in force at the moment of dispensing, snapshotted for Billing (HO-ADM-001 ADR-049: never re-read).</summary>
public sealed record PriceSnapshot
{
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public string CurrencyCode { get; init; }

    public PriceSnapshot(decimal unitPrice, decimal lineTotal, string currencyCode)
    {
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
        CurrencyCode = currencyCode;
    }

    // EF only.
    private PriceSnapshot() { CurrencyCode = default!; }
}
