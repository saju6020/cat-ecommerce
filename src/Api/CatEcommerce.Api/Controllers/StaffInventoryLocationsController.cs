using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 -- the active branch's stock locations. A location of another branch is a 404, never a leak.</summary>
[ApiController]
[Route("api/staff/inventory/locations")]
public sealed class StaffInventoryLocationsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List()
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStockLocationsQuery
        { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.StockLocations.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateStockLocationRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var locationId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateStockLocationCommand
        {
            LocationId = locationId, BranchId = branchId, Code = request.Code, Name = request.Name, Kind = request.Kind,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.CreatedOrFailure(response, new { locationId });
    }

    [HttpPut("{id:guid}")]
    [RequireAction(Actions.StockLocations.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStockLocationRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new UpdateStockLocationCommand
        {
            LocationId = id, BranchId = branchId, Name = request.Name,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    /// <summary>Deactivation is refused while any position at the location holds stock (<c>StockLocationHasStock</c>); a location is never deleted.</summary>
    [HttpPut("{id:guid}/activation")]
    [RequireAction(Actions.StockLocations.Manage)]
    public async Task<IActionResult> SetActivation(Guid id, [FromBody] SetStockLocationActivationRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SetStockLocationActivationCommand
        {
            LocationId = id, BranchId = branchId, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }
}
