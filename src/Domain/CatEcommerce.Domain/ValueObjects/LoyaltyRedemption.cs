namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-BIL-001 §8.6/§8.11 -- the points redeemed on a <b>draft</b> invoice and their value,
/// with the ledger entry that recorded the redemption. Apportioned across the qualifying lines at
/// issue (<c>InvoiceDiscountShare</c>).</summary>
public sealed record LoyaltyRedemption
{
    public int Points { get; init; }
    public decimal Value { get; init; }
    public Guid LoyaltyEntryId { get; init; }

    public LoyaltyRedemption(int points, decimal value, Guid loyaltyEntryId)
    {
        Points = points;
        Value = value;
        LoyaltyEntryId = loyaltyEntryId;
    }

    private LoyaltyRedemption()
    {
    }
}
