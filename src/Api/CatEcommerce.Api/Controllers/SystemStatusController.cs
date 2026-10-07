using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Administration;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 / ADR-053 -- public status; never gates itself.</summary>
[ApiController]
[Route("api/system")]
public sealed class SystemStatusController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("status")]
    [PublicEndpoint]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetSystemStatusQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }
}
