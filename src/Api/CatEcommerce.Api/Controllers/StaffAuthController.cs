using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Account;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Api.Sessions;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using AccountSignInResult = CatEcommerce.Shared.SharedDto.Account.SignInResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ACC-001 §12.4 / US-ACC-14. [PublicEndpoint] markers added by HO-ADM-001 §12.3 --
/// every action here runs ahead of any staff-role check by construction (sign-in/sign-out/reset
/// request are how a staff session is obtained or given up in the first place).</summary>
[ApiController]
[Route("api/staff/auth")]
public sealed class StaffAuthController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    ISessionTokenIssuer sessionTokenIssuer,
    SessionCookieWriter cookieWriter)
    : ControllerBase
{
    [HttpPost("sign-in")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [CsrfExempt]
    [PublicEndpoint]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
    {
        var command = new SignInStaffCommand
        {
            SessionId = Guid.NewGuid(),
            Identifier = request.Identifier,
            Password = request.Password,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        if (!response.IsSuccess)
            return StatusCode(401, new { error = "invalid_credentials", message = AccountMessages.SignInFailed });

        var result = (AccountSignInResult)response.Result!;
        cookieWriter.WriteStaffSession(HttpContext, result.Token, result.ExpiresOn);
        cookieWriter.WriteCsrf(HttpContext, result.CsrfToken, result.ExpiresOn);

        return Ok(new { staffAccountId = result.AccountId, expiresOn = result.ExpiresOn });
    }

    [HttpPost("sign-out")]
    [PublicEndpoint]
    public new async Task<IActionResult> SignOut()
    {
        var token = Request.Cookies[SessionCookieWriter.StaffSessionCookie];
        if (!string.IsNullOrEmpty(token))
        {
            await dispatcher.SendAsync(new SignOutStaffCommand
            {
                TokenHash = sessionTokenIssuer.HashToken(token),
                UserContext = userContextProvider.GetUserContext(),
                CorrelationId = Guid.NewGuid(),
                ValidateCommand = true
            });
        }

        cookieWriter.ExpireStaffSession(HttpContext);
        cookieWriter.ExpireCsrf(HttpContext);

        return NoContent();
    }

    [HttpPost("password-reset/request")]
    [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
    [PublicEndpoint]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestRequest request)
    {
        var command = new RequestPasswordResetCommand
        {
            TokenId = Guid.NewGuid(),
            AccountKind = AccountKind.Staff,
            Identifier = request.Identifier,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        await dispatcher.SendAsync(command);

        return Ok(new { message = AccountMessages.PasswordResetRequested });
    }
}
