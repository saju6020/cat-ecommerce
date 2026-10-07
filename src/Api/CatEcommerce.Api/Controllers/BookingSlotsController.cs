using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Clinic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-APT-001 §12.4 -- public availability. Never computes availability in SQL.</summary>
[ApiController]
[Route("api/booking")]
public sealed class BookingSlotsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("slots")]
    [PublicEndpoint]
    [EnableRateLimiting(AppointmentRateLimitPolicies.Availability)]
    public async Task<IActionResult> GetSlots([FromQuery] Guid branchId, [FromQuery] Guid serviceId, [FromQuery] DateOnly date, [FromQuery] Guid? performerId, [FromQuery] Guid? catId)
    {
        // catId is honoured only with a customer session (§12.4) -- an anonymous or staff caller
        // never gets a cat-scoped exclusion here.
        var caller = HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;
        var effectiveCatId = caller.Kind == ResolvedCallerKind.Customer ? catId : null;

        var response = await dispatcher.GetResultAsync(new GetAvailableSlotsQuery
        {
            BranchId = branchId, ServiceId = serviceId, Date = date, PerformerId = performerId, CatId = effectiveCatId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpGet("performers")]
    [PublicEndpoint]
    [EnableRateLimiting(AppointmentRateLimitPolicies.Performers)]
    public async Task<IActionResult> GetPerformers([FromQuery] Guid branchId, [FromQuery] Guid serviceId)
    {
        var response = await dispatcher.GetResultAsync(new GetBookingPerformersQuery
        { BranchId = branchId, ServiceId = serviceId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }
}
