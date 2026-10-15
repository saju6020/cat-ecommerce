using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One batch received on a consignment: its ACCEPTED quantity and what it was bought at (in the purchase currency).</summary>
public sealed record LandedCostBatchInput(Guid BatchId, decimal AcceptedQuantity, decimal PurchaseUnitCostAmount, string PurchaseCurrencyCode);

/// <summary>One consignment cost component (freight, duty, ...) in its own currency.</summary>
public sealed record LandedCostComponentInput(decimal Amount, string CurrencyCode);

/// <summary>The allocation, or the business refusal the handler turns into a 400. <c>Property</c> names the offending currency for <c>ExchangeRateMissing</c>.</summary>
public sealed record LandedCostResult(
    IReadOnlyList<LandedCostLineDto> Lines, decimal TotalCostsBase, string? ViolationCode, string? Message)
{
    public bool IsSuccess => ViolationCode is null;

    public static LandedCostResult Refused(string code, string message) => new([], 0m, code, message);
}

/// <summary>
/// HO-IMP-001 §8.11 / ADR-106 (US-IMP-05) -- landed cost as a PURE domain service. The basis is the accepted quantity (OQ-3). Every amount is converted to base
/// currency with the consignment's manual rates and rounded to two decimals; the total of the cost components is shared across the batches in minor units
/// by LARGEST REMAINDER (ties go to the earlier batch), so the allocated amounts add up to the total exactly. The landed unit cost is a four-decimal
/// <c>UnitCost</c>, not money.
/// </summary>
public sealed class LandedCostAllocator
{
    public LandedCostResult Allocate(
        IReadOnlyList<LandedCostBatchInput> batches, IReadOnlyList<LandedCostComponentInput> components, IReadOnlyDictionary<string, decimal> ratesToBase,
        string baseCurrency)
    {
        var received = batches.Where(b => b.AcceptedQuantity > 0m).ToList();
        if (received.Count == 0)
            return LandedCostResult.Refused(BusinessViolationCodes.NoReceivedQuantityToAllocate, "There is no received quantity on this consignment to allocate costs to.");

        var currencies = received.Select(b => b.PurchaseCurrencyCode).Concat(components.Select(c => c.CurrencyCode)).Distinct(StringComparer.Ordinal);
        foreach (var currency in currencies)
        {
            if (currency != baseCurrency && !ratesToBase.ContainsKey(currency))
                return LandedCostResult.Refused(BusinessViolationCodes.ExchangeRateMissing, $"No exchange rate to {baseCurrency} is recorded for {currency}.");
        }

        decimal Rate(string currency) => currency == baseCurrency ? 1m : ratesToBase[currency];

        var totalCostsBase = components.Sum(c => Round2(c.Amount * Rate(c.CurrencyCode)));
        var shares = Share(totalCostsBase, received.Select(b => b.AcceptedQuantity).ToList());

        var lines = new List<LandedCostLineDto>(received.Count);
        for (var i = 0; i < received.Count; i++)
        {
            var batch = received[i];
            var purchaseBase = Round2(batch.AcceptedQuantity * batch.PurchaseUnitCostAmount * Rate(batch.PurchaseCurrencyCode));
            var landedUnit = decimal.Round((purchaseBase + shares[i]) / batch.AcceptedQuantity, 4, MidpointRounding.ToEven);
            lines.Add(new LandedCostLineDto(batch.BatchId, batch.AcceptedQuantity, purchaseBase, shares[i], landedUnit));
        }

        return new LandedCostResult(lines, totalCostsBase, null, null);
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven);

    /// <summary>Splits <paramref name="total"/> (two decimals) across the quantities in minor units; the leftover cents go to the largest remainders.</summary>
    private static List<decimal> Share(decimal total, IReadOnlyList<decimal> quantities)
    {
        var totalMinor = (long)decimal.Round(total * 100m, 0, MidpointRounding.ToEven);
        var sum = quantities.Sum();
        var exact = quantities.Select(q => totalMinor * q / sum).ToList();
        var floors = exact.Select(e => (long)decimal.Floor(e)).ToList();

        var leftover = totalMinor - floors.Sum();
        var order = Enumerable.Range(0, quantities.Count).OrderByDescending(i => exact[i] - floors[i]).ThenBy(i => i).Take((int)leftover);
        foreach (var i in order) floors[i]++;

        return floors.Select(minor => minor / 100m).ToList();
    }
}
