using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Product;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- the signed-in customer's view of the catalogue.
/// Personal, so <c>no-store</c>; <c>[StorefrontEndpoint]</c> refuses in maintenance mode (ADR-093).</summary>
[ApiController]
[Route("api/customers/me/shop")]
[RequireCustomerSession]
[StorefrontEndpoint]
public sealed class CustomerShopController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    /// <summary>US-SHP-10 AC1 -- per-SKU purchasability with the reason and the eligible cats.</summary>
    [HttpGet("products/{slug}/purchasability")]
    public async Task<IActionResult> GetPurchasability(string slug)
    {
        var userContext = userContextProvider.GetUserContext();
        Response.Headers.CacheControl = "private, no-store";

        var response = await dispatcher.GetResultAsync(new GetPurchasabilityQuery
        { CustomerId = userContext.UserId, Slug = slug, UserContext = userContext, CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
