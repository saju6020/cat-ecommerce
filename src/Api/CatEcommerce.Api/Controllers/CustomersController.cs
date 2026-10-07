using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Account;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ACC-001 §12.4 / US-ACC-01. [PublicEndpoint] added by HO-ADM-001 §12.3.</summary>
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpPost]
    [ServiceFilter(typeof(CaptchaValidationFilter))]
    [EnableRateLimiting(RateLimitPolicies.Registration)]
    [PublicEndpoint]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerRequest request)
    {
        var customerId = Guid.NewGuid();
        var command = new RegisterCustomerCommand
        {
            CustomerId = customerId,
            FullName = request.FullName,
            MobileNumber = request.MobileNumber,
            EmailAddress = request.EmailAddress,
            Password = request.Password,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess
            ? StatusCode(201, new { customerId })
            : BadRequest(response.ValidationResult);
    }
}
