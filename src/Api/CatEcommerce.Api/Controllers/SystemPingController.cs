using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-XCT-001 section 12.4 (US-XCT-01 AC3). A cheap reachability probe for the browser's offline detector. Public, <c>204</c>, no body, never cached,
/// and passive: it never records staff activity, so it cannot keep an idle session alive.
/// </summary>
[ApiController]
public sealed class SystemPingController : ControllerBase
{
    [HttpGet("api/system/ping")]
    [PublicEndpoint]
    [PassiveSession]
    [EnableRateLimiting(RateLimitPolicies.Ping)]
    public IActionResult Ping()
    {
        Response.Headers.CacheControl = "no-store";
        return NoContent();
    }
}
