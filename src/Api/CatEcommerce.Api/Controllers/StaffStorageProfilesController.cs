using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §8.4 / §12.4 (US-IMP-16 part 1) -- Inventory's own record of how a product must be kept. Keyed by the product, not the
/// branch.</summary>
[ApiController]
[Route("api/staff/inventory/storage-profiles")]
public sealed class StaffStorageProfilesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("{productId:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Get(Guid productId)
    {
        var response = await dispatcher.GetResultAsync(new GetStorageProfileQuery
        { ProductId = productId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{productId:guid}")]
    [RequireAction(Actions.ProductStorage.Manage)]
    public async Task<IActionResult> Set(Guid productId, [FromBody] SetProductStorageProfileRequest request)
    {
        var response = await dispatcher.SendAsync(new SetProductStorageProfileCommand
        {
            ProductId = productId, Condition = request.Condition, MinTemperatureC = request.MinTemperatureC, MaxTemperatureC = request.MaxTemperatureC,
            HandlingNotes = request.HandlingNotes, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }
}
