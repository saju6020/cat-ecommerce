using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Read.Queries.Administration;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- a customer's own policy acceptances.</summary>
[ApiController]
[Route("api/customers/me/policy-acceptances")]
[RequireCustomerSession]
public sealed class CustomerPolicyAcceptancesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var userContext = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetPendingPolicyAcceptancesQuery { CustomerAccountId = userContext.UserId, UserContext = userContext, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost]
    public async Task<IActionResult> Accept([FromBody] AcceptPolicyVersionRequest request)
    {
        var command = new AcceptPolicyVersionCommand
        {
            PolicyKey = request.PolicyKey, VersionNumber = request.VersionNumber,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
