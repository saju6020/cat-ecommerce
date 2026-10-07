using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Sessions;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Account;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ACC-001 §12.4 / US-ACC-08 / ADR-037. [PublicEndpoint] markers (HO-ADM-001 §12.3) --
/// these are session-read endpoints; a stale or missing cookie is refused inside the action itself
/// (401 <c>session_invalid</c>), not by a role check.</summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffSessionController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    ISessionTokenIssuer sessionTokenIssuer,
    ISessionPolicy sessionPolicy,
    TimeProvider timeProvider)
    : ControllerBase
{
    /// <summary>Never extends the idle window -- <see cref="PassiveSessionAttribute"/> tells the
    /// middleware not to dispatch the activity heartbeat for this action.</summary>
    [HttpGet("session")]
    [PassiveSession]
    [PublicEndpoint]
    public async Task<IActionResult> GetStatus() => await GetStatusInternalAsync();

    [HttpPost("session/activity")]
    [PublicEndpoint]
    public async Task<IActionResult> RecordActivity() => await GetStatusInternalAsync();

    private async Task<IActionResult> GetStatusInternalAsync()
    {
        var token = Request.Cookies[SessionCookieWriter.StaffSessionCookie];
        if (string.IsNullOrEmpty(token))
        {
            Response.Headers.CacheControl = "no-store";
            return StatusCode(401, new { error = "session_invalid" });
        }

        var response = await dispatcher.GetResultAsync(new GetStaffSessionStatusQuery
        {
            TokenHash = sessionTokenIssuer.HashToken(token),
            AsOf = timeProvider.GetUtcNow(),
            IdleTimeoutMinutes = (int)sessionPolicy.StaffIdleTimeout.TotalMinutes,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        if (response.Result is null)
        {
            Response.Headers.CacheControl = "no-store";
            return StatusCode(401, new { error = "session_invalid" });
        }

        return Ok(response.Result);
    }
}
