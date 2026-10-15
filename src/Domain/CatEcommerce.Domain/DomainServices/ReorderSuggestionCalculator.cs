namespace CatEcommerce.Domain.DomainServices;

/// <summary>What one (SKU, location) pair needs to be judged: consumption over the window, the lead time and cover, the point, and the position
/// (usable stock plus what is still due on issued purchase orders).</summary>
public sealed record ReorderInput(
    decimal ConsumedInWindow, int WindowDays, int LeadTimeDays, int CoverDays, decimal ReorderPoint, decimal Usable, decimal OnOrder, decimal? PolicyQuantity);

public sealed record ReorderSuggestion(decimal AverageDailyConsumption, int CoverDaysTotal, decimal Position, decimal SuggestedQuantity);

/// <summary>
/// HO-IMP-001 §8.13 (US-IMP-12 AC2) -- the reorder suggestion as a PURE domain service:
/// <code>
/// avgDaily  = consumed in the window / window days
/// cover     = lead time + cover days
/// position  = usable + on order
/// suggested = the policy's reorder quantity when set,
///             otherwise max(0, ceil(avgDaily x cover) + reorderPoint - position)   -- whole units
/// </code>
/// </summary>
public sealed class ReorderSuggestionCalculator
{
    public ReorderSuggestion Suggest(ReorderInput input)
    {
        var avgDaily = input.WindowDays <= 0 ? 0m : input.ConsumedInWindow / input.WindowDays;
        var cover = input.LeadTimeDays + input.CoverDays;
        var position = input.Usable + input.OnOrder;

        var suggested = input.PolicyQuantity
            ?? Math.Max(0m, Math.Ceiling(avgDaily * cover) + input.ReorderPoint - position);

        return new ReorderSuggestion(avgDaily, cover, position, suggested);
    }
}
