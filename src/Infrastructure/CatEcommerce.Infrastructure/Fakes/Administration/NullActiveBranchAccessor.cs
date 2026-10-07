using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Infrastructure.Fakes.Administration;

/// <summary>
/// FAKE until HO-ADM-001 lands -- replace this registration.
///
/// Always <see cref="ActiveBranchResult.NoBranch"/> -- mirrors the real rule's own "customer or
/// anonymous → no active branch" case (§8.3 rule 4), the safest default when no branch/assignment
/// data exists yet.
/// </summary>
public sealed class NullActiveBranchAccessor : IActiveBranchAccessor
{
    public ActiveBranchResult GetActiveBranchId() => ActiveBranchResult.NoBranch;
}
