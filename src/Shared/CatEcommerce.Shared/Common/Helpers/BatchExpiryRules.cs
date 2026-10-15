namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-IMP-001 §8.6.1 / ADR-101 -- THE one expiry predicate in C#. "Expired" is derived, never stored: a batch is
/// usable through the whole of its expiry date (OQ-20) and expired from the next branch-local day. The database
/// trigger <c>tg_stock_batch_usable</c> mirrors it in SQL (<see cref="SqlComparison"/>); U-X2 checks the migration
/// text against this class.
/// </summary>
public static class BatchExpiryRules
{
    /// <summary>The comparison operator the SQL mirror must use: <c>branch_today &gt; expires_on</c>, never <c>&gt;=</c>.</summary>
    public const string SqlComparison = ">";

    public static bool IsExpired(DateOnly? expiresOn, DateOnly branchToday) =>
        expiresOn is not null && branchToday > expiresOn.Value;

    /// <summary>Whole days from <paramref name="branchToday"/> to expiry; negative once expired; null when undated.</summary>
    public static int? DaysToExpiry(DateOnly? expiresOn, DateOnly branchToday) =>
        expiresOn is null ? null : expiresOn.Value.DayNumber - branchToday.DayNumber;
}
