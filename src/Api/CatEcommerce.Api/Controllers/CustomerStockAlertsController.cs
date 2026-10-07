using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Product;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- the caller's own back-in-stock alerts (US-SHP-13). Every
/// handler resolves by id AND caller: another customer's alert is a 404.</summary>
[ApiController]
[Route("api/customers/me/stock-alerts")]
[RequireCustomerSession]
[StorefrontEndpoint]
[EnableRateLimiting(ShopRateLimitPolicies.StockAlerts)]
public sealed class CustomerStockAlertsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userContext = userContextProvider.GetUserContext();
        Response.Headers.CacheControl = "no-store";

        var response = await dispatcher.GetResultAsync(new GetMyStockAlertsQuery
        { CustomerId = userContext.UserId, UserContext = userContext, CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    /// <summary>201 with the alert id and <c>deliverability</c> -- the real routing decision for
    /// <c>shop.back-in-stock</c> (F33, OQ-2), so the client can say when nothing will reach the
    /// customer (no marketing consent on any channel).</summary>
    [HttpPost]
    public async Task<IActionResult> RequestAlert([FromBody] RequestStockAlertRequest request)
    {
        var userContext = userContextProvider.GetUserContext();
        var stockAlertId = Guid.NewGuid();

        var response = await dispatcher.SendAsync(new RequestStockAlertCommand
        { StockAlertId = stockAlertId, SkuId = request.SkuId, UserContext = userContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess)
        {
            var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.SkuNotFound) ?? false;
            return notFound ? NotFound() : BadRequest(response.ValidationResult);
        }

        var deliverability = await dispatcher.GetResultAsync(new GetStockAlertDeliverabilityQuery
        { CustomerId = userContext.UserId, UserContext = userContext, CorrelationId = Guid.NewGuid() });

        Response.Headers.CacheControl = "no-store";
        return StatusCode(201, new { stockAlertId, deliverability = deliverability.Result });
    }

    [HttpDelete("{stockAlertId:guid}")]
    public async Task<IActionResult> Cancel(Guid stockAlertId)
    {
        var response = await dispatcher.SendAsync(new CancelStockAlertCommand
        { StockAlertId = stockAlertId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (response.IsSuccess) return NoContent();

        var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.StockAlertNotFound) ?? false;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }
}
