using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Portal;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-POR-001 §12.4 -- the customer portal. Injects the dispatcher and the user-context provider ONLY. Every action carries
/// <c>[RequireCustomerSession]</c> (a staff session is a 401), dispatches exactly one query, and the caller is whatever the session
/// says: the query has no customer-id property a client could set (ADR-261). Nothing here pays anything (ADR-264).
/// </summary>
[ApiController]
[Route("api/portal")]
public sealed class PortalController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("dashboard")]
    [RequireCustomerSession]
    public async Task<IActionResult> GetDashboard()
    {
        var response = await dispatcher.GetResultAsync(new GetPortalDashboardQuery
        {
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
        });

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }
}
