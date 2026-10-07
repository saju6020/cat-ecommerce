using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-APT-001 §12.4/§8.7 -- the queue is a read, recomputed on every call.</summary>
[ApiController]
[Route("api/staff/queue")]
public sealed class StaffQueueController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Appointments.View)]
    public async Task<IActionResult> Get([FromQuery] string? performerId)
    {
        var active = activeBranchAccessor.GetActiveBranchId();
        if (active.BranchId is not { } branchId)
            return StatusCode(403, new { error = active.Refusal?.ToString() ?? "branch_required" });

        Guid? resolvedPerformerId = null;
        if (string.Equals(performerId, "me", StringComparison.OrdinalIgnoreCase))
        {
            resolvedPerformerId = userContextProvider.GetUserContext().UserId;
        }
        else if (!string.IsNullOrWhiteSpace(performerId))
        {
            if (!Guid.TryParse(performerId, out var parsed)) return BadRequest(new { error = "performerId must be a guid or 'me'." });
            resolvedPerformerId = parsed;
        }

        var response = await dispatcher.GetResultAsync(new GetClinicQueueQuery
        { BranchId = branchId, PerformerId = resolvedPerformerId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }
}
