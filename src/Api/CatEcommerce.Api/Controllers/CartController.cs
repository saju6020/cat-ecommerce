using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Order;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 (US-ORD-01, ADR-199) -- the server-side cart for guests (an opaque <c>pc_cart</c> cookie) and customers (their one active cart,
/// on any device). Cart endpoints are public and storefront (maintenance-gated); writes are rate limited (<c>cart</c>, 120/min per IP). The caller is
/// never in a body or a route: a customer is the session, a guest is the cookie. A cart that is not yours does not exist (<c>404</c>).
/// </summary>
[ApiController]
[StorefrontEndpoint]
public sealed class CartController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, GuestCartCookieWriter cookies, IGuestCartTokens tokens) : ControllerBase
{
    /// <summary>The caller's cart. No cookie and no session is an empty cart, and <b>no cookie is set</b>.</summary>
    [HttpGet("api/cart")]
    [PublicEndpoint]
    public async Task<IActionResult> Get()
    {
        Response.Headers.CacheControl = "no-store";
        var user = userContextProvider.GetUserContext();

        var response = await dispatcher.GetResultAsync(new GetCartQuery
        {
            CustomerId = GuestCartCookieWriter.IsCustomer(user) ? user.UserId : null,
            GuestTokenHash = cookies.ReadHash(Request),
            UserContext = user,
            CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpPost("api/cart/lines")]
    [PublicEndpoint]
    [EnableRateLimiting(RateLimitPolicies.Cart)]
    public async Task<IActionResult> AddLine([FromBody] AddCartLineRequest request)
    {
        var user = userContextProvider.GetUserContext();
        var candidate = GuestCartCookieWriter.IsCustomer(user) ? null : tokens.Mint();

        var response = await dispatcher.SendAsync(new AddCartLineCommand
        {
            NewCartId = Guid.NewGuid(), GuestTokenHash = cookies.ReadHash(Request), NewGuestTokenHash = candidate?.Hash,
            SkuId = request.SkuId, Quantity = request.Quantity, CatId = request.CatId,
            UserContext = user, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var result = (AddCartLineResult)response.Result!;
        if (result.GuestCartCreated && candidate is not null) cookies.Write(Response, candidate.Token);

        Response.Headers.CacheControl = "no-store";
        return StatusCode(201, new { lineId = result.LineId, cartId = result.CartId });
    }

    [HttpPut("api/cart/lines/{lineId:guid}")]
    [PublicEndpoint]
    [EnableRateLimiting(RateLimitPolicies.Cart)]
    public async Task<IActionResult> UpdateLine(Guid lineId, [FromBody] UpdateCartLineRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateCartLineCommand
        {
            LineId = lineId, GuestTokenHash = cookies.ReadHash(Request), Quantity = request.Quantity, CatId = request.CatId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    [HttpDelete("api/cart/lines/{lineId:guid}")]
    [PublicEndpoint]
    [EnableRateLimiting(RateLimitPolicies.Cart)]
    public async Task<IActionResult> RemoveLine(Guid lineId)
    {
        var response = await dispatcher.SendAsync(new RemoveCartLineCommand
        {
            LineId = lineId, GuestTokenHash = cookies.ReadHash(Request),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>Merges the guest cart named by the cookie into the signed-in customer's cart (ADR-199) -- an explicit, idempotent <c>POST</c>, never a
    /// side effect of a <c>GET</c>. The cookie is expired either way: after a merge (or when there was nothing to merge) it is no longer needed.</summary>
    [HttpPost("api/customers/me/cart/merge")]
    [RequireCustomerSession]
    [EnableRateLimiting(RateLimitPolicies.Cart)]
    public async Task<IActionResult> Merge()
    {
        var hash = cookies.ReadHash(Request);
        if (hash is null)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(new MergeGuestCartResult(0, []));
        }

        var response = await dispatcher.SendAsync(new MergeGuestCartCommand
        {
            NewCartId = Guid.NewGuid(), GuestTokenHash = hash,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        cookies.Expire(Response);
        Response.Headers.CacheControl = "no-store";
        var result = (MergeGuestCartResult)response.Result!;
        return Ok(new { mergedLineCount = result.MergedLineCount, droppedLines = result.DroppedLines });
    }
}
