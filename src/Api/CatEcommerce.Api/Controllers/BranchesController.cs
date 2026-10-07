using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Administration;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4. The clinic contact details US-CLN-04 shows.</summary>
[ApiController]
[Route("api/branches")]
public sealed class BranchesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [PublicEndpoint]
    public async Task<IActionResult> GetBranches()
    {
        var response = await dispatcher.GetResultAsync(new GetBranchesQuery
        {
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }
}
