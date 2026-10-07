namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ADM-001, gate fix 2026-09-26. A handful of admin read lists have no natural bound and were
/// never paginated (my-leave-requests; closures and roster exceptions when no date range is given;
/// a service's price history; a tax class's rate history; the policy list with its version
/// history) -- each is capped here rather than converted to full <c>PagedResult</c> paging, since
/// none of these lists is expected to legitimately exceed a few dozen rows in real operation (a
/// service does not accumulate hundreds of prices); a hard cap closes the unbounded-scan risk
/// without changing any of these five endpoints' response shape.
/// </summary>
public static class AdministrationListCaps
{
    public const int MaxUnboundedListSize = 100;
}
