using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Product;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- SKU prices, barcode lookup (HW-2) and reference data.</summary>
[ApiController]
[Route("api/staff/shop")]
public sealed class StaffSkusController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("skus/{skuId:guid}/prices")]
    [RequireAction(Actions.Shop.ProductsView)]
    public async Task<IActionResult> GetPriceHistory(Guid skuId)
    {
        var response = await dispatcher.GetResultAsync(new GetSkuPriceHistoryQuery
        { SkuId = skuId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("skus/{skuId:guid}/prices")]
    [RequireAction(Actions.Shop.ProductsManagePrices)]
    public async Task<IActionResult> SchedulePrice(Guid skuId, [FromBody] ScheduleSkuPriceRequest request)
    {
        var skuPriceId = Guid.NewGuid();
        var command = new ScheduleSkuPriceCommand
        {
            SkuPriceId = skuPriceId, SkuId = skuId, Amount = request.Amount, CurrencyCode = request.CurrencyCode,
            EffectiveFrom = request.EffectiveFrom, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { skuPriceId }) : BadRequest(response.ValidationResult);
    }

    [HttpGet("skus")]
    [RequireAction(Actions.Shop.ProductsView)]
    public async Task<IActionResult> FindByBarcode([FromQuery] string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return BadRequest(new { error = "barcode is required." });

        var response = await dispatcher.GetResultAsync(new FindSkuByBarcodeQuery
        { Barcode = barcode, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("countries")]
    [RequireAction(Actions.Shop.ProductsView)]
    public async Task<IActionResult> GetCountries()
    {
        var response = await dispatcher.GetResultAsync(new GetCountriesQuery
        { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }
}
