using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.8, US-ORD-06 AC1 -- pure, in minor units. The delivery charge from the zone, the order value, the weight and the service level
/// (each service level carries its own rules): <c>goodsValue = Σ(subtotal - lineDiscount) - orderDiscount</c>; if a free-over threshold is set and
/// <c>goodsValue &gt;=</c> it, the charge is 0; otherwise <c>BaseCharge</c> plus the surcharge of the first weight band whose limit the total weight
/// does not exceed (an open-ended band takes every heavier parcel). A SKU without a weight counts as 0 grams (the caller sums the weights). The
/// delivery charge is untaxed (OQ-6).
/// </summary>
public static class DeliveryChargeCalculator
{
    public static MinorUnits Calculate(ZoneChargeRules rules, MinorUnits goodsValue, long totalWeightGrams)
    {
        if (rules.FreeOverOrderValue is { } freeOver && goodsValue >= MinorUnits.FromAmount(freeOver))
            return MinorUnits.Zero;

        var charge = MinorUnits.FromAmount(rules.BaseCharge);

        var band = rules.WeightBands.FirstOrDefault(b => b.UpToGrams is null || totalWeightGrams <= b.UpToGrams);
        return band is null ? charge : charge + MinorUnits.FromAmount(band.Surcharge);
    }
}
