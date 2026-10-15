namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>HO-IMP-001 §8.13 -- US-IMP-10's thresholds. A batch raises only the TIGHTEST threshold it has crossed, so one
/// received with 45 days left raises 60, later 30, and never a retrospective 90.</summary>
public static class ExpiryAlertRules
{
    /// <summary>The distinct non-zero values of the three settings, sorted descending (0 = off).</summary>
    public static IReadOnlyList<int> Thresholds(int first, int second, int third) =>
        new[] { first, second, third }.Where(t => t > 0).Distinct().OrderByDescending(t => t).ToList();

    /// <summary><c>t* = min{ t : daysLeft &lt;= t }</c>; null when no threshold is crossed yet or the batch is already
    /// expired (<c>daysLeft &lt; 0</c> raises the separate <c>Expired</c> alert instead).</summary>
    public static int? TightestCrossed(int daysLeft, IReadOnlyList<int> thresholds)
    {
        if (daysLeft < 0) return null;
        var crossed = thresholds.Where(t => daysLeft <= t).ToList();
        return crossed.Count == 0 ? null : crossed.Min();
    }
}
