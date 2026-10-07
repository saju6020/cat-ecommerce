using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Account;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Api.Sessions;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Read.Queries.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ACC-001 §12.4 / US-ACC-06 / US-ACC-13. Every action here requires a customer session; the
/// customer id always comes from <c>UserContext.UserId</c>, never the request body (§11.1).
/// </summary>
[ApiController]
[Route("api/customers/me")]
[RequireCustomerSession]
public sealed class CustomerProfileController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    SessionCookieWriter sessionCookieWriter) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerProfileQuery
        {
            CustomerId = userContextProvider.GetUserContext().UserId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    // SECURITY gate MEDIUM (second pass, HO-ACC-001 §1 Amended (18)); ARCHITECTURE gate fifth
    // pass, MEDIUM, follow-up (2026-09-25): this action carries the email step-up's password
    // re-check whenever the request changes the email (see
    // UpdateCustomerContactDetailsCommandHandler), so the whole endpoint is rate-limited. It now
    // has its own policy (RateLimitPolicies.ProfileUpdate, same 5-per-60-minutes numbers as
    // AccountSensitive), partitioned by the signed-in customer id rather than IP -- unlike
    // AccountSensitive (deletion, export), which stays IP-partitioned. A name-only or
    // phone-list-only edit is bound by the same limit as a side effect; that is judged an
    // acceptable trade-off over adding a second endpoint for a still-rare, interactive action
    // (§1 Amended (18)). The customer partition also means repeated profile saves can no longer
    // block that same customer's own deletion/export attempt (different bucket), and two
    // customers behind shared NAT or clinic Wi-Fi no longer share one bucket.
    [HttpPut]
    [EnableRateLimiting(RateLimitPolicies.ProfileUpdate)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCustomerProfileRequest request)
    {
        var command = new UpdateCustomerContactDetailsCommand
        {
            FullName = request.FullName,
            EmailAddress = request.EmailAddress,
            AdditionalPhoneNumbers = request.AdditionalPhoneNumbers,
            CurrentPassword = request.CurrentPassword,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses()
    {
        var response = await dispatcher.GetResultAsync(new GetDeliveryAddressesQuery
        {
            CustomerId = userContextProvider.GetUserContext().UserId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromBody] DeliveryAddressRequest request)
    {
        var addressId = Guid.NewGuid();
        var command = new AddDeliveryAddressCommand
        {
            AddressId = addressId,
            Label = request.Label,
            Line1 = request.Line1,
            Line2 = request.Line2,
            City = request.City,
            District = request.District,
            PostalCode = request.PostalCode,
            DeliveryInstructions = request.DeliveryInstructions,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? StatusCode(201, new { addressId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("addresses/{addressId:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] DeliveryAddressRequest request)
    {
        var command = new UpdateDeliveryAddressCommand
        {
            AddressId = addressId,
            Label = request.Label,
            Line1 = request.Line1,
            Line2 = request.Line2,
            City = request.City,
            District = request.District,
            PostalCode = request.PostalCode,
            DeliveryInstructions = request.DeliveryInstructions,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpDelete("addresses/{addressId:guid}")]
    public async Task<IActionResult> RemoveAddress(Guid addressId)
    {
        var command = new RemoveDeliveryAddressCommand
        {
            AddressId = addressId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPut("addresses/{addressId:guid}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId)
    {
        var command = new SetDefaultDeliveryAddressCommand
        {
            AddressId = addressId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        var response = await dispatcher.GetResultAsync(new GetNotificationPreferencesQuery
        {
            CustomerId = userContextProvider.GetUserContext().UserId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpPut("notification-preferences")]
    public async Task<IActionResult> ChangeNotificationPreferences([FromBody] ChangeNotificationPreferencesRequest request)
    {
        var command = new ChangeNotificationPreferencesCommand
        {
            Changes = request.Changes
                .Select(c => new NotificationPreferenceChangeItem { Channel = c.Channel, Category = c.Category, IsGranted = c.IsGranted })
                .ToList(),
            PreferredChannel = request.PreferredChannel,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("personal-data-export")]
    [EnableRateLimiting(RateLimitPolicies.AccountSensitive)]
    public async Task<IActionResult> ExportPersonalData()
    {
        var userContext = userContextProvider.GetUserContext();

        var recordResponse = await dispatcher.SendAsync(new RecordPersonalDataExportCommand
        {
            UserContext = userContext,
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        });

        if (!recordResponse.IsSuccess) return BadRequest(recordResponse.ValidationResult);

        var exportResponse = await dispatcher.GetResultAsync(new GetPersonalDataExportQuery
        {
            CustomerId = userContext.UserId,
            UserContext = userContext,
            CorrelationId = Guid.NewGuid()
        });

        Response.Headers.CacheControl = "no-store";
        Response.Headers.ContentDisposition = "attachment; filename=\"purrcare-my-data.json\"";

        return Ok(exportResponse.Result);
    }

    [HttpPost("deletion")]
    [EnableRateLimiting(RateLimitPolicies.AccountSensitive)]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        var command = new DeleteCustomerAccountCommand
        {
            CurrentPassword = request.CurrentPassword,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (!response.IsSuccess) return BadRequest(response.ValidationResult);

        // SECURITY gate item 7 (first-pass gate fix round) -- a bare `Cookies.Delete(name)` never
        // carries the `Domain` `pc_csrf` may have originally been set with (`AccountOptions.
        // CsrfCookieDomain`); the browser then treats it as a different cookie entirely (host-only
        // vs domain-scoped are separate jars for the same name/path) and never actually removes
        // the real one. `SessionCookieWriter`'s own `Expire*` methods are the one place that
        // knows the right `Domain` for each cookie -- used here instead of a raw `Cookies.Delete`.
        sessionCookieWriter.ExpireCustomerSession(HttpContext);
        sessionCookieWriter.ExpireCsrf(HttpContext);

        return NoContent();
    }
}
