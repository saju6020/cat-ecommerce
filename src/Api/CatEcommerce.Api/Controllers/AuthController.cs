using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Account;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Api.Sessions;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using AccountSignInResult = CatEcommerce.Shared.SharedDto.Account.SignInResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ACC-001 §12.4 / US-ACC-04 / US-ACC-05.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    ISessionTokenIssuer sessionTokenIssuer,
    SessionCookieWriter cookieWriter,
    TimeProvider timeProvider)
    : ControllerBase
{
    [HttpPost("sign-in")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [CsrfExempt]
    [PublicEndpoint]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
    {
        var command = new SignInCustomerCommand
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
        {
            // R1 -- one generic failure body, no Retry-After.
            return StatusCode(401, new { error = "invalid_credentials", message = AccountMessages.SignInFailed });
        }

        var result = (AccountSignInResult)response.Result!;
        cookieWriter.WriteCustomerSession(HttpContext, result.Token, result.ExpiresOn);
        cookieWriter.WriteCsrf(HttpContext, result.CsrfToken, result.ExpiresOn);

        return Ok(new { customerId = result.AccountId, expiresOn = result.ExpiresOn });
    }

    [HttpPost("sign-out")]
    [RequireCustomerSession]
    public new async Task<IActionResult> SignOut()
    {
        var token = Request.Cookies[SessionCookieWriter.CustomerSessionCookie];
        if (!string.IsNullOrEmpty(token))
        {
            var command = new SignOutCustomerCommand
            {
                TokenHash = sessionTokenIssuer.HashToken(token),
                UserContext = userContextProvider.GetUserContext(),
                CorrelationId = Guid.NewGuid(),
                ValidateCommand = true
            };
            await dispatcher.SendAsync(command);
        }

        cookieWriter.ExpireCustomerSession(HttpContext);
        cookieWriter.ExpireCsrf(HttpContext);

        return NoContent();
    }

    [HttpGet("session")]
    [PublicEndpoint]
    public async Task<IActionResult> GetSession()
    {
        var token = Request.Cookies[SessionCookieWriter.CustomerSessionCookie];
        if (string.IsNullOrEmpty(token))
        {
            Response.Headers.CacheControl = "no-store";
            return StatusCode(401, new { error = "session_invalid" });
        }

        var query = new GetCurrentCustomerSessionQuery
        {
            TokenHash = sessionTokenIssuer.HashToken(token),
            AsOf = timeProvider.GetUtcNow(),
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        };

        var response = await dispatcher.GetResultAsync(query);

        if (response.Result is null)
        {
            Response.Headers.CacheControl = "no-store";
            return StatusCode(401, new { error = "session_invalid" });
        }

        return Ok(response.Result);
    }

    [HttpPost("password-reset/request")]
    [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
    [PublicEndpoint]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestRequest request) =>
        await RequestPasswordResetInternalAsync(request, AccountKind.Customer);

    [HttpPost("password-reset/confirm")]
    [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
    [PublicEndpoint]
    public async Task<IActionResult> ConfirmPasswordReset([FromBody] PasswordResetConfirmRequest request)
    {
        var command = new ResetPasswordCommand
        {
            Token = request.Token,
            NewPassword = request.NewPassword,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    internal async Task<IActionResult> RequestPasswordResetInternalAsync(PasswordResetRequestRequest request, AccountKind kind)
    {
        var command = new RequestPasswordResetCommand
        {
            TokenId = Guid.NewGuid(),
            AccountKind = kind,
            Identifier = request.Identifier,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        await dispatcher.SendAsync(command);

        // R3 -- one neutral body, identical on every outcome.
        return Ok(new { message = AccountMessages.PasswordResetRequested });
    }
}
