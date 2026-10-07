using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Administration;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- public policy read.</summary>
[ApiController]
[Route("api/policies")]
public sealed class PoliciesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("{key}")]
    [PublicEndpoint]
    public async Task<IActionResult> Get(string key)
    {
        var response = await dispatcher.GetResultAsync(new GetPublicPolicyQuery { Key = key, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
