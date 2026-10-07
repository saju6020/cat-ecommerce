using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Notification;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-NTF-001 §12.4/§8.11 -- US-NTF-04 AC1. Public: no session, exempt from CSRF (HO-ACC-001 R7
/// applies only when a session cookie is presented). Neither action logs the token itself -- the
/// dispatch pipeline's own logging is id/code-only (§8.9 rule 5) and this host has no
/// query-string-logging middleware to begin with.
/// </summary>
[ApiController]
[Route("api/notifications/unsubscribe")]
public sealed class NotificationUnsubscribeController(IInMemoryDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    [PublicEndpoint]
    [EnableRateLimiting(NotificationRateLimitPolicies.Unsubscribe)]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        var response = await Dispatch(request.Token);
        if (!response.IsSuccess) return BadRequest(response.ValidationResult);

        var outcome = (UnsubscribeOutcome)response.Result!;
        return Ok(new { channel = outcome.Channel, wasSubscribed = outcome.WasSubscribed });
    }

    /// <summary>RFC 8058 one-click: <c>List-Unsubscribe-Post: List-Unsubscribe=One-Click</c> in
    /// the form body; the token rides in the URL because RFC 8058 requires it there. Empty 200
    /// body -- no confirmation content is meaningful to the mail client that posts this.</summary>
    [HttpPost("one-click")]
    [PublicEndpoint]
    [EnableRateLimiting(NotificationRateLimitPolicies.Unsubscribe)]
    public async Task<IActionResult> OneClickUnsubscribe([FromQuery] string token)
    {
        var response = await Dispatch(token);
        if (!response.IsSuccess) return BadRequest(response.ValidationResult);

        return Ok();
    }

    private Task<CommandResponse> Dispatch(string token) =>
        dispatcher.SendAsync(new UnsubscribeFromMarketingCommand
        {
            Token = token,
            UserContext = new UserContext(SystemActors.NotificationWorker, Guid.Empty),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        });
}
