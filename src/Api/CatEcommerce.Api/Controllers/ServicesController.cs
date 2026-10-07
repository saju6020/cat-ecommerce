using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Administration;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- public, priced, bookable-online services.</summary>
[ApiController]
[Route("api/services")]
public sealed class ServicesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [PublicEndpoint]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetPublicServicesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }
}
