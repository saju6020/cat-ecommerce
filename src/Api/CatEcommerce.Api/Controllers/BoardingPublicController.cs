using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BRD-001 section 12.4 -- the public side of boarding: the price list of unit types (and, from Phase C, availability). Never writes.</summary>
[ApiController]
[Route("api/boarding")]
public sealed class BoardingPublicController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    [HttpGet("availability")]
    [PublicEndpoint]
    [EnableRateLimiting(BoardingRateLimitPolicies.Availability)]
    public async Task<IActionResult> GetAvailability([FromQuery] Guid branchId, [FromQuery] DateOnly arrivalOn, [FromQuery] DateOnly departureOn, [FromQuery] int catCount)
    {
        var response = await Dispatcher.GetResultAsync(new GetBoardingAvailabilityQuery
        {
            BranchId = branchId, ArrivalOn = arrivalOn, DepartureOn = departureOn, CatCount = catCount, IsStaff = false, UserContext = Caller, CorrelationId = Guid.NewGuid()
        });
        return Ok(response.Result);
    }

    [HttpGet("unit-types")]
    [PublicEndpoint]
    public async Task<IActionResult> GetUnitTypes([FromQuery] Guid? branchId)
    {
        var response = await Dispatcher.GetResultAsync(new GetBoardingUnitTypesQuery { BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }
}
