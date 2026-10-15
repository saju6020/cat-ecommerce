using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Surgery;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-SRG-001 section 12.4 (US-SRG-07, OQ-12) -- the owner's view of a surgery: what is booked, fasting, after discharge the post-op instructions and follow-ups. Customers only see
/// their own; someone else's surgery is the same 404 as a missing one. Recovery progress and clinical notes are not shown (reception relays them).
/// </summary>
[ApiController]
[Route("api/customers/me/surgeries")]
[RequireCustomerSession]
public sealed class CustomerSurgeriesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? scope, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var user = userContextProvider.GetUserContext();
        var query = new GetCustomerSurgeriesQuery
        { CustomerId = user.UserId, Scope = string.IsNullOrWhiteSpace(scope) ? "upcoming" : scope, UserContext = user, CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerSurgeryDetailQuery { CustomerId = user.UserId, SurgeryId = id, UserContext = user, CorrelationId = Guid.NewGuid() });
        if (response.Result is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }
}
