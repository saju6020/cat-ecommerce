using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-RPT-001 section 12.4 (US-RPT-05, FR-RPT-3) -- the role home dashboard: the tiles for the caller's roles, each already gated server-side, for today at the active branch (X-Branch-Id).</summary>
[ApiController]
[Route("api/staff/dashboard")]
public sealed class StaffDashboardController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Dashboard.View)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(CatEcommerce.Api.RateLimiting.ReportRateLimitPolicies.Dashboard)]
    public async Task<IActionResult> Get()
    {
        var caller = HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
        var active = activeBranchAccessor.GetActiveBranchId();

        var response = await dispatcher.GetResultAsync(new GetDashboardQuery
        {
            CallerRoles = caller is { Kind: ResolvedCallerKind.Staff } ? caller.RoleIds : [], CallerId = caller?.AccountId ?? Guid.Empty, ActiveBranchId = active.BranchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        Response.Headers.CacheControl = "no-store";
        if (response.Result!.IsRefused)
            return StatusCode(403, new { error = BusinessViolationCodes.BranchNotAssigned, errors = new[] { new { errorCode = BusinessViolationCodes.BranchNotAssigned, errorMessage = ReportingMessages.BranchNotAssigned } } });

        return Ok(response.Result.Result);
    }
}
