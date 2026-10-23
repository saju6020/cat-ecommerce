using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- staff branch assignment.</summary>
[ApiController]
public sealed class StaffBranchesController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet("api/admin/staff/{staffAccountId:guid}/branches")]
    [RequireAction(Actions.Branches.View)]
    public async Task<IActionResult> GetAssignment(Guid staffAccountId)
    {
        var response = await dispatcher.GetResultAsync(new GetStaffBranchAssignmentQuery { StaffAccountId = staffAccountId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        if (response.Result is not null) return Ok(response.Result);

        // Owner, 2026-10-21: a staff member not yet assigned to any branch gets an empty assignment (what the web
        // panel and its mock expect), so the administrator can pick their branches; 404 only for an unknown account.
        var staff = await dispatcher.GetResultAsync(new GetStaffAccountQuery { StaffAccountId = staffAccountId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return staff.Result is null
            ? NotFound()
            : Ok(new { staffAccountId, branchIds = Array.Empty<Guid>(), primaryBranchId = (Guid?)null });
    }

    [HttpPut("api/admin/staff/{staffAccountId:guid}/branches")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> Assign(Guid staffAccountId, [FromBody] AssignStaffBranchesRequest request)
    {
        var command = new AssignStaffBranchesCommand
        {
            StaffAccountId = staffAccountId, BranchIds = request.BranchIds, PrimaryBranchId = request.PrimaryBranchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    /// <summary>The branch picker: the caller's own assignment plus the branch resolved for the
    /// current request (honouring <c>X-Branch-Id</c>). Any staff role -- guarded by
    /// <see cref="Actions.Branches.ViewOwn"/>, not <see cref="PublicEndpointAttribute"/>, so a
    /// customer calling it gets a <c>403</c> with an audit row, not a hand-rolled <c>401</c>
    /// (gate fix 2026-09-26).
    ///
    /// Gate fix 2026-09-26 (ARCH MEDIUM, second pass): this endpoint's own payload -- the caller's
    /// assigned branches and primary -- never needed an active branch (ADR-046: "an endpoint that
    /// does not need a branch never fails on a missing or bad header"). It previously turned a
    /// refused <c>X-Branch-Id</c> into a hard <c>403</c>, which broke exactly the picker a stale
    /// header should let the frontend recover through. It now always returns <c>200</c> with
    /// <c>branchIds</c>/<c>primaryBranchId</c>, plus <c>activeBranchId</c> (<c>null</c> when
    /// refused) and <c>activeBranchRefusal</c> (the refusal code, or <c>null</c>) alongside.</summary>
    [HttpGet("api/staff/branches/mine")]
    [RequireAction(Actions.Branches.ViewOwn)]
    public async Task<IActionResult> Mine()
    {
        var caller = HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;

        var response = await dispatcher.GetResultAsync(new GetMyBranchesQuery
        {
            StaffAccountId = caller.AccountId,
            // Owner, 2026-10-21: an Administrator works in any active branch.
            IncludeAllActiveBranches = caller.RoleIds.Contains(StaffRoles.Administrator, StringComparer.Ordinal),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        var active = activeBranchAccessor.GetActiveBranchId();

        return response.Result is null
            ? Ok(new { branchIds = Array.Empty<Guid>(), primaryBranchId = (Guid?)null, activeBranchId = active.BranchId, activeBranchRefusal = active.Refusal?.ToString() })
            : Ok(new { response.Result.BranchIds, response.Result.PrimaryBranchId, activeBranchId = active.BranchId, activeBranchRefusal = active.Refusal?.ToString() });
    }
}
