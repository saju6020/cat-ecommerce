namespace CatEcommerce.Domain.DomainServices;

/// <summary>Where a courier-collected COD payment stands against the remittances that cover it.</summary>
public enum CodCollectionStatus
{
    /// <summary>Nothing remitted yet.</summary>
    Unremitted,

    /// <summary>Something remitted, but less than was collected -- the difference is the shortfall.</summary>
    Shortfall,

    /// <summary>Everything collected was remitted.</summary>
    Remitted,
}

/// <summary>
/// HO-BIL-001 ADR-189 -- pure. The shortfall of a courier-collected COD payment is <c>collected - sum(remitted)</c> across <b>all</b>
/// remittances once the courier has remitted anything (U-G6: collected 1200, remitted 1000 then 150, shortfall 50); a collection with
/// nothing remitted is <i>unremitted</i>, not a shortfall.
/// </summary>
public static class CodShortfall
{
    public static decimal OutstandingOf(decimal collected, decimal remitted) => Math.Max(0m, collected - remitted);

    public static CodCollectionStatus StatusOf(decimal collected, decimal remitted) =>
        remitted <= 0m ? CodCollectionStatus.Unremitted : remitted >= collected ? CodCollectionStatus.Remitted : CodCollectionStatus.Shortfall;

    public static decimal ShortfallOf(decimal collected, decimal remitted) =>
        StatusOf(collected, remitted) == CodCollectionStatus.Shortfall ? OutstandingOf(collected, remitted) : 0m;

    /// <summary>Older than the due window and still not fully remitted (<c>billing.codRemittanceDueDays</c>).</summary>
    public static bool IsOverdue(DateOnly receivedOn, DateOnly today, int dueDays, decimal collected, decimal remitted) =>
        remitted < collected && receivedOn.AddDays(dueDays) < today;
}
