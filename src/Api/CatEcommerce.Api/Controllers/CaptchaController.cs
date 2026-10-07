using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Captcha;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Infrastructure.Security.Captcha;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ACC-001 §12.4 / ADR-029. Injects the CAPTCHA service directly -- a recorded exception to
/// "controllers inject the dispatcher and user context only" because a challenge is not a domain
/// command (ADR-029). [PublicEndpoint] added by HO-ADM-001 §12.3.
/// </summary>
[ApiController]
[Route("api/captcha")]
public sealed class CaptchaController(ICaptchaService captchaService) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.CaptchaIssue)]
    [PublicEndpoint]
    public IActionResult Issue()
    {
        var challenge = captchaService.Issue();
        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(challenge.ImageBytes)}";

        Response.Headers.CacheControl = "no-store";

        return Ok(new CaptchaResponse(challenge.CaptchaId, dataUrl, challenge.ExpiresInSeconds));
    }
}
