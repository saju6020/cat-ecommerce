namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 section 8.11 (ADR-191) -- pure loyalty arithmetic. Points accrue on the <b>qualifying net</b> of a paid invoice (after every discount,
/// before tax, fee lines excluded -- OQ-8): <c>floor(net / spendPerPoint)</c>. A credit note takes back the points its qualifying net earned,
/// never more than the invoice still has accrued. A redemption is worth <c>points x pointValue</c>, rounded as every other amount.
/// </summary>
public static class LoyaltyCalculator
{
    public static int AccrualPoints(decimal qualifyingNet, decimal spendPerPoint)
    {
        if (qualifyingNet <= 0m || spendPerPoint <= 0m) return 0;

        return (int)Math.Min(int.MaxValue, Math.Floor(qualifyingNet / spendPerPoint));
    }

    /// <summary>The points a credit gives back: what its qualifying net would have earned, capped at what the invoice still holds.</summary>
    public static int ReversalPoints(decimal creditedQualifyingNet, decimal spendPerPoint, int accruedAndNotYetReversed) =>
        Math.Min(AccrualPoints(creditedQualifyingNet, spendPerPoint), Math.Max(0, accruedAndNotYetReversed));

    public static decimal RedemptionValue(int points, decimal pointValue) => InvoiceCalculator.Round(points * pointValue);
}
