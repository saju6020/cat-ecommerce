using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 -- SKU lookup for receiving, by barcode (exact) or by text (at most 20 rows). Reads the catalogue only through
/// <c>ISkuDirectory</c> (ADR-111) and returns each SKU's <c>storage</c> (US-IMP-16 AC1).</summary>
[ApiController]
[Route("api/staff/inventory/skus")]
public sealed class StaffSkuLookupController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Lookup([FromQuery] string? search, [FromQuery] string? barcode)
    {
        if (string.IsNullOrWhiteSpace(search) && string.IsNullOrWhiteSpace(barcode))
            return this.InvalidQuery("search", "Give a search text or a barcode.");

        var response = await dispatcher.GetResultAsync(new GetSkuLookupQuery
        { Search = search, Barcode = barcode, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }
}
