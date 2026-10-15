using CatEcommerce.Domain.DomainServices;
using Microsoft.AspNetCore.Mvc;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 -- what the staff Ordering controllers share: the active branch (HO-ADM-001 §8.3, ADR-046 -- a refused <c>X-Branch-Id</c> is a
/// <c>403</c> naming the refusal, never silently downgraded). An order of another branch is simply not found by the handler. Abstract, so MVC never routes it.
/// </summary>
public abstract class StaffOrderControllerBase(IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    protected bool TryResolveBranch(out Guid branchId, out IActionResult? refusal)
    {
        var active = activeBranchAccessor.GetActiveBranchId();
        if (active.BranchId is { } id)
        {
            branchId = id;
            refusal = null;
            return true;
        }

        branchId = default;
        refusal = StatusCode(403, new { error = active.Refusal?.ToString() ?? "branch_required" });
        return false;
    }
}
