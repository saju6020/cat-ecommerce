using CatEcommerce.Api.Authorization;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- the caller's own wishlist (US-SHP-13). There is no customer id
/// in any route or body: the wishlist is the caller's, so another customer's is unreachable.</summary>
[ApiController]
[Route("api/customers/me/wishlist")]
[RequireCustomerSession]
[StorefrontEndpoint]
public sealed class CustomerWishlistController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userContext = userContextProvider.GetUserContext();
        Response.Headers.CacheControl = "no-store";

        var response = await dispatcher.GetResultAsync(new GetWishlistQuery
        { CustomerId = userContext.UserId, UserContext = userContext, CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    /// <summary>Idempotent: adding a SKU that is already there is a 204.</summary>
    [HttpPut("items/{skuId:guid}")]
    public async Task<IActionResult> Add(Guid skuId)
    {
        var response = await dispatcher.SendAsync(new AddWishlistItemCommand
        { SkuId = skuId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (response.IsSuccess) return NoContent();

        var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.SkuNotFound) ?? false;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }

    [HttpDelete("items/{skuId:guid}")]
    public async Task<IActionResult> Remove(Guid skuId)
    {
        var response = await dispatcher.SendAsync(new RemoveWishlistItemCommand
        { SkuId = skuId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
