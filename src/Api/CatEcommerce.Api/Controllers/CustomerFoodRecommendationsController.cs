using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 / 8.13 -- US-SHP-11. Personal, so <c>no-store</c>. The cat must be
/// the caller's and alive: another customer's cat (or a deceased one) is a 404, indistinguishable
/// from an unknown id. The guidance is general, pending clinical sign-off (OQ-9) -- the client says so.</summary>
[ApiController]
[Route("api/customers/me/cats/{catId:guid}/food-recommendations")]
[RequireCustomerSession]
[StorefrontEndpoint]
[EnableRateLimiting(ShopRateLimitPolicies.FoodRecommendations)]
public sealed class CustomerFoodRecommendationsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid catId, [FromQuery] string? locale)
    {
        var userContext = userContextProvider.GetUserContext();
        Response.Headers.CacheControl = "no-store";

        var response = await dispatcher.GetResultAsync(new GetFoodRecommendationsQuery
        { CustomerId = userContext.UserId, CatId = catId, Locale = locale, UserContext = userContext, CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
