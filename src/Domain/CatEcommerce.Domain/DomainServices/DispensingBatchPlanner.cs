using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.7 (ADR-143, BR-3, BR-4) -- FEFO batch planning and the rules a requested dispensing must meet. Pure: Inventory's facts go in
/// (<see cref="BatchOffer"/>), a plan or a list of violations comes out.
///
/// <b>There is no override.</b> An expired batch (<c>ExpiresOn &lt; today</c>, branch-local) is never suggested and never accepted; no parameter,
/// role or flag changes that (U-E3 reflects over this class's API to prove it). The only deviation a reason can excuse is choosing a different batch
/// than FEFO would (<c>FefoOverrideReason</c>, BR-4) -- never an unusable one.
/// </summary>
public static class DispensingBatchPlanner
{
    /// <summary>The server's suggestion: dispensable batches by expiry then lot, filled greedily, at most five lines.</summary>
    public static DispensePlan Plan(decimal quantity, IReadOnlyList<BatchOffer> offers, DateOnly today)
    {
        var dispensable = DispensableInFefoOrder(offers, today);
        var lines = new List<DispenseLineInput>();
        var remaining = quantity;

        foreach (var offer in dispensable)
        {
            if (remaining <= 0m || lines.Count >= DispensingRules.MaxLinesPerDispensing) break;

            var take = Math.Min(remaining, offer.Available);
            lines.Add(new DispenseLineInput(offer.SkuId, offer.BatchId, take));
            remaining -= take;
        }

        var totalDispensable = dispensable.Sum(o => o.Available);
        var expired = offers.Where(o => IsExpired(o, today) && o.Available > 0m).OrderBy(o => o.ExpiresOn).FirstOrDefault();
        var onlyExpired = totalDispensable <= 0m && expired is not null;

        return new DispensePlan(lines, onlyExpired, totalDispensable, Math.Max(0m, remaining), expired);
    }

    /// <summary>All reasons the requested lines cannot be dispensed for <paramref name="requiredQuantity"/>; empty when they can. Checked in the order a
    /// dispenser would want them explained.</summary>
    public static IReadOnlyList<DispenseViolation> Validate(
        IReadOnlyList<DispenseLineInput> requested, decimal requiredQuantity, IReadOnlyList<BatchOffer> offers, IReadOnlyCollection<Guid> linkedSkuIds,
        DateOnly today, string? fefoOverrideReason)
    {
        var violations = new List<DispenseViolation>();

        if (requested.Count == 0 || requested.Count > DispensingRules.MaxLinesPerDispensing)
        {
            violations.Add(new DispenseViolation(BusinessViolationCodes.TooManyDispensingLines, "Lines",
                $"A supply is dispensed in one to {DispensingRules.MaxLinesPerDispensing} batches."));
            return violations;
        }

        foreach (var line in requested)
        {
            if (!linkedSkuIds.Contains(line.SkuId))
            {
                violations.Add(new DispenseViolation(BusinessViolationCodes.SkuNotLinkedToMedicine, "Lines", "That SKU is not a stocked form of this medicine."));
                continue;
            }

            var offer = offers.FirstOrDefault(o => o.BatchId == line.BatchId && o.SkuId == line.SkuId);
            if (offer is null)
            {
                violations.Add(new DispenseViolation(BusinessViolationCodes.BatchNotDispensable, "Lines", "That batch is not in this branch's dispensary."));
                continue;
            }

            if (IsExpired(offer, today))
            {
                violations.Add(new DispenseViolation(BusinessViolationCodes.BatchExpired, "Lines", PharmacyMessages.BatchExpired(offer.LotNumber, offer.ExpiresOn!.Value)));
                continue;
            }

            if (!offer.IsDispensable)
            {
                violations.Add(new DispenseViolation(BusinessViolationCodes.BatchNotDispensable, "Lines", offer.NotDispensableReason ?? "That batch cannot be dispensed."));
                continue;
            }

            if (line.Quantity <= 0m || line.Quantity > offer.Available)
            {
                violations.Add(new DispenseViolation(BusinessViolationCodes.InsufficientDispensaryStock, "Lines", PharmacyMessages.InsufficientDispensaryStock(offer.Available)));
            }
        }

        if (violations.Count > 0) return violations;

        if (requested.Sum(l => l.Quantity) != requiredQuantity)
        {
            violations.Add(new DispenseViolation(BusinessViolationCodes.DispenseQuantityMismatch, "Lines",
                "A supply is dispensed in full: the lines must add up to the prescribed quantity."));
            return violations;
        }

        var fefo = Plan(requiredQuantity, offers.Where(o => linkedSkuIds.Contains(o.SkuId)).ToList(), today);
        if (!SamePlan(fefo.SuggestedLines, requested) && (fefoOverrideReason is null || fefoOverrideReason.Trim().Length < DispensingRules.MinFefoOverrideReasonLength))
        {
            violations.Add(new DispenseViolation(BusinessViolationCodes.FefoOverrideReasonRequired, "FefoOverrideReason",
                "That is not the first-expired-first-out choice. Give a reason of at least five characters."));
        }

        return violations;
    }

    public static bool IsExpired(BatchOffer offer, DateOnly today) => offer.ExpiresOn is { } expiresOn && expiresOn < today;

    private static List<BatchOffer> DispensableInFefoOrder(IReadOnlyList<BatchOffer> offers, DateOnly today) =>
        offers
            .Where(o => o.IsDispensable && !IsExpired(o, today) && o.Available > 0m)
            .OrderBy(o => o.ExpiresOn is null).ThenBy(o => o.ExpiresOn).ThenBy(o => o.LotNumber, StringComparer.Ordinal).ThenBy(o => o.BatchId)
            .ToList();

    private static bool SamePlan(IReadOnlyList<DispenseLineInput> fefo, IReadOnlyList<DispenseLineInput> chosen)
    {
        static List<(Guid, decimal)> Key(IEnumerable<DispenseLineInput> lines) =>
            lines.GroupBy(l => l.BatchId).Select(g => (g.Key, g.Sum(l => l.Quantity))).OrderBy(x => x.Key).ToList();

        return Key(fefo).SequenceEqual(Key(chosen));
    }
}
