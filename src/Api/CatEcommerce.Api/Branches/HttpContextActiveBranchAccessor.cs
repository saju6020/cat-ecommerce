using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Persistence;
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

        if (httpContext.Request.Headers.TryGetValue(BranchHeaderName, out var headerValue))
        {
            if (!Guid.TryParse(headerValue.ToString(), out var requestedBranchId))
                return ActiveBranchResult.RefusedBy(ActiveBranchRefusal.BranchNotAssigned);

            return assignment is not null && assignment.BranchIds.Contains(requestedBranchId)
                ? ActiveBranchResult.Resolved(requestedBranchId)
                : ActiveBranchResult.RefusedBy(ActiveBranchRefusal.BranchNotAssigned);
        }

        if (assignment is not null) return ActiveBranchResult.Resolved(assignment.PrimaryBranchId);

        var activeBranchIds = db.Set<BranchAggregate>().Where(b => b.IsActive).Select(b => b.Id).ToList();
        return activeBranchIds.Count == 1
            ? ActiveBranchResult.Resolved(activeBranchIds[0])
            : ActiveBranchResult.RefusedBy(ActiveBranchRefusal.BranchAssignmentRequired);
    }
}
