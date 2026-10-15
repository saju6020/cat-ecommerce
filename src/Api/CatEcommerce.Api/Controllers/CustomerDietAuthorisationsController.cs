using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Prescription;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-PHM-001 §12.4 (US-PHM-08, FR-POR-3) -- the owner's diet authorisations, only for the caller's CURRENT cats (an authorisation of someone else's cat is never listed).</summary>
[ApiController]
[RequireCustomerSession]
[Route("api/customers/me")]
public sealed class CustomerDietAuthorisationsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet("diet-authorisations")]
    public async Task<IActionResult> Get([FromQuery] Guid? catId, [FromQuery] string? scope)
    {
        if (scope is not null && scope is not ("active" or "past")) return BadRequest(new { error = "scope must be 'active' or 'past'." });

        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerDietAuthorisationsQuery
        { CustomerId = user.UserId, Scope = scope ?? "active", CatId = catId, UserContext = user, CorrelationId = Guid.NewGuid() });

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }
}
