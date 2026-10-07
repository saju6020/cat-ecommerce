namespace CatEcommerce.Domain.DomainServices;

/// <summary>Why an endpoint could not resolve an active branch for a staff caller (ADR-046).</summary>
public enum ActiveBranchRefusal
{
    /// <summary>An <c>X-Branch-Id</c> header was sent, but it does not parse as a <c>Guid</c>, or it
    /// names a branch the caller is not assigned to.</summary>
    BranchNotAssigned,

    /// <summary>No header, no assignment, and either zero or more than one active branch exists, so
    /// there is no safe default.</summary>
    BranchAssignmentRequired
}

/// <summary>
/// The outcome of resolving the caller's active branch (HO-ADM-001 §8.3, ADR-046). Exactly one of
/// three shapes: a resolved <see cref="BranchId"/>; no branch and no refusal (the caller is a
/// customer or anonymous -- endpoints that do not need a branch never fail on this); or a
/// <see cref="Refusal"/>, which the calling controller must turn into a <c>403</c> naming the
/// refusal code. Gate fix 2026-09-26: previously this collapsed both "no branch" cases (customer)
/// and "refused" cases (bad header, unassigned, ambiguous default) into a single <c>null</c>,
/// which a consumer could not tell apart -- an unparsable or out-of-assignment header silently fell
/// back to the primary branch instead of being refused.
/// </summary>
public readonly record struct ActiveBranchResult(Guid? BranchId, ActiveBranchRefusal? Refusal)
{
    public static readonly ActiveBranchResult NoBranch = new(null, null);

    public static ActiveBranchResult Resolved(Guid branchId) => new(branchId, null);

    public static ActiveBranchResult RefusedBy(ActiveBranchRefusal refusal) => new(null, refusal);
}

/// <summary>
/// Owned by HO-ADM-001 §8.3/§8.9 (ADR-046), Api-layer/Scoped in the real implementation. Resolves
/// the caller's active branch from the <c>X-Branch-Id</c> header, primary assignment, or the single
/// active branch, per §8.3's four rules.
///
/// Until HO-ADM-001 lands, <c>Fakes/Administration/NullActiveBranchAccessor</c> always returns
/// <see cref="ActiveBranchResult.NoBranch"/> -- mirrors the real rule's own "customer or anonymous
/// → no active branch" case, the safest default when no branch/assignment data exists yet.
/// </summary>
public interface IActiveBranchAccessor
{
    ActiveBranchResult GetActiveBranchId();
}
