using System.Globalization;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 (US-ORD-02 to US-ORD-07, ADR-200) -- the checkout quote for the signed-in customer. A <c>GET</c> that changes nothing: it re-prices the
/// cart, lists every change since the items were added, offers the delivery options and payment methods with their reasons, and returns the fingerprint
/// the customer must send back to place the order. Customer session, storefront (maintenance-gated), rate limited (<c>checkout</c>, 10/min per IP,
/// NFR-SEC-6). The customer is the session -- there is no customer id in the route or query.
/// </summary>
[ApiController]
[Route("api/customers/me/checkout")]
[RequireCustomerSession]
[StorefrontEndpoint]
public sealed class CustomerCheckoutController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("quote")]
    [EnableRateLimiting(RateLimitPolicies.Checkout)]
    public async Task<IActionResult> Quote(
        [FromQuery] string fulfilment, [FromQuery] Guid? addressId, [FromQuery] string? serviceLevel, [FromQuery] string? slotDate,
        [FromQuery] string? slotWindowStart, [FromQuery] Guid? pickupBranchId, [FromQuery] string? paymentMethod, [FromQuery] string? discountCode)
    {
        Response.Headers.CacheControl = "no-store";

        if (!EnumQueryParameter.TryParse<FulfilmentMethod>(fulfilment, out var method) || method is null || method == FulfilmentMethod.Counter)
            return BadRequest(new { error = "fulfilment must be Delivery or Pickup." });

        if (!EnumQueryParameter.TryParse<DeliveryServiceLevel>(serviceLevel, out var level)) return BadRequest(new { error = "serviceLevel must be Standard or Express." });
        if (!EnumQueryParameter.TryParse<OrderPaymentMethod>(paymentMethod, out var payment) || payment == OrderPaymentMethod.InStore)
            return BadRequest(new { error = "paymentMethod must be CashOnDelivery, BankTransfer or PayOnCollection." });

        DateOnly? date = null;
        if (!string.IsNullOrWhiteSpace(slotDate))
        {
            if (!DateOnly.TryParseExact(slotDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return BadRequest(new { error = "slotDate must be yyyy-MM-dd." });
            date = parsed;
        }

        TimeOnly? start = null;
        if (!string.IsNullOrWhiteSpace(slotWindowStart))
        {
            if (!TimeOnly.TryParseExact(slotWindowStart, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return BadRequest(new { error = "slotWindowStart must be HH:mm." });
            start = parsed;
        }

        if (discountCode is { Length: > 40 }) return BadRequest(new { error = "discountCode is too long." });

        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCheckoutQuoteQuery
        {
            CustomerId = user.UserId,
            Choice = new FulfilmentChoice(method, addressId, level, date, start, pickupBranchId, payment, string.IsNullOrWhiteSpace(discountCode) ? null : discountCode.Trim()),
            UserContext = user,
            CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }
}
