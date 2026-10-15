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

/// <summary>HO-IMP-001 §12.4 (US-IMP-13 AC1) -- stock transfers of the ACTIVE branch: two ledger entries per line, one commit. A transfer of stock that is
/// no longer free is a <c>409 InsufficientStock</c> from the projection's CHECK (ADR-099); a destination in another branch needs the caller assigned
/// to it.</summary>
[ApiController]
[Route("api/staff/inventory/transfers")]
public sealed class StaffTransfersController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetStockTransfersQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Stock.Transfer)]
    public async Task<IActionResult> Post([FromBody] PostStockTransferRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var transferId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new PostStockTransferCommand
        {
            TransferId = transferId, BranchId = branchId, FromLocationId = request.FromLocationId, ToLocationId = request.ToLocationId, Reason = request.Reason,
            Lines = request.ToLines(), UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.CreatedOrFailure(response, new { transferId, number = response.Result as string });
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStockTransferQuery
        { TransferId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
