using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Http;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Api.Branches;

/// <summary>
/// HO-ADM-001 §8.3 / ADR-046. Resolution:
/// 1. Staff caller with header <c>X-Branch-Id</c> -&gt; must parse and be in their assignment -&gt;
///    else <see cref="ActiveBranchRefusal.BranchNotAssigned"/>. A garbage or out-of-assignment
///    header is refused, never silently replaced by the primary branch (gate fix 2026-09-26).
/// 2. Staff caller without the header -&gt; <c>PrimaryBranchId</c>.
/// 3. Staff caller with no assignment -&gt; the only active branch if exactly one exists;
///    otherwise <see cref="ActiveBranchRefusal.BranchAssignmentRequired"/>.
/// 4. Customer or anonymous -&gt; <see cref="ActiveBranchResult.NoBranch"/> (not a refusal).
///
/// Owner, 2026-10-21: an Administrator may work in ANY active branch -- the header may name any active branch, and
/// with no assignment and no header they get the first active branch by name. A staff caller with no assignment may
/// also send the one active branch rule 3 would have given them (the web app sends back the branch it was told).
///
/// The calling controller decides what a refusal means for that endpoint -- turning it into a
/// <c>403</c> naming the refusal code, per ADR-046 and §8.3.
/// </summary>
public sealed class HttpContextActiveBranchAccessor(IHttpContextAccessor httpContextAccessor, CatEcommerceDbContext db) : IActiveBranchAccessor
{
    public const string BranchHeaderName = "X-Branch-Id";

    public ActiveBranchResult GetActiveBranchId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return ActiveBranchResult.NoBranch;

        var caller = httpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;
        if (caller.Kind != ResolvedCallerKind.Staff) return ActiveBranchResult.NoBranch;

        var assignment = db.Set<StaffBranchAssignmentAggregate>().Find(caller.AccountId);
        var isAdministrator = caller.RoleIds.Contains(StaffRoles.Administrator, StringComparer.Ordinal);

        if (httpContext.Request.Headers.TryGetValue(BranchHeaderName, out var headerValue))
        {
            if (!Guid.TryParse(headerValue.ToString(), out var requestedBranchId))
                return ActiveBranchResult.RefusedBy(ActiveBranchRefusal.BranchNotAssigned);

            if (assignment is not null && assignment.BranchIds.Contains(requestedBranchId))
                return ActiveBranchResult.Resolved(requestedBranchId);

            if (isAdministrator || assignment is null)
            {
                var activeIds = ActiveBranchIds();
                var allowed = isAdministrator ? activeIds.Contains(requestedBranchId) : activeIds is [var only] && only == requestedBranchId;
                if (allowed) return ActiveBranchResult.Resolved(requestedBranchId);
            }

            return ActiveBranchResult.RefusedBy(ActiveBranchRefusal.BranchNotAssigned);
        }

        if (assignment is not null) return ActiveBranchResult.Resolved(assignment.PrimaryBranchId);

        var activeBranchIds = ActiveBranchIds();
        if (activeBranchIds.Count == 1) return ActiveBranchResult.Resolved(activeBranchIds[0]);
        return isAdministrator && activeBranchIds.Count > 0
            ? ActiveBranchResult.Resolved(activeBranchIds[0])
            : ActiveBranchResult.RefusedBy(ActiveBranchRefusal.BranchAssignmentRequired);
    }

    /// <summary>Active branches, first by name (the Administrator's default when unassigned).</summary>
    private List<Guid> ActiveBranchIds() =>
        db.Set<BranchAggregate>().Where(b => b.IsActive).OrderBy(b => b.Name).Select(b => b.Id).ToList();
}
