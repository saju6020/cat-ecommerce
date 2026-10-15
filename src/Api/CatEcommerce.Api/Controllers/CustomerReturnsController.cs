using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Order;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ORD-001 §12.4 (US-ORD-13) -- a return of the signed-in customer's. By id and caller: someone else's return is <c>404</c> (§12.3).</summary>
[ApiController]
[Route("api/customers/me/returns")]
[RequireCustomerSession]
public sealed class CustomerReturnsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("{returnRequestId:guid}")]
    public async Task<IActionResult> Get(Guid returnRequestId)
    {
        Response.Headers.CacheControl = "no-store";
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerReturnQuery { CustomerId = user.UserId, ReturnRequestId = returnRequestId, UserContext = user, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
