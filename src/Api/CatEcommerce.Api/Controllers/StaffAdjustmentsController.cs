using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 (US-IMP-13 AC2, ADR-107) -- stock adjustments: a reason AND a note are mandatory; above the branch's threshold the
/// adjustment waits for a DIFFERENT approver holding <c>Stock.ApproveAdjustment</c> and writes no ledger entry until then. Approving after the stock
/// has moved is a <c>409 InsufficientStock</c>.</summary>
[ApiController]
[Route("api/staff/inventory/adjustments")]
public sealed class StaffAdjustmentsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetStockAdjustmentsQuery { BranchId = branchId, Status = status, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Stock.Adjust)]
    public async Task<IActionResult> RequestAdjustment([FromBody] RequestStockAdjustmentRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var adjustmentId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RequestStockAdjustmentCommand
        {
            AdjustmentId = adjustmentId, BranchId = branchId, LocationId = request.LocationId, Reason = request.Reason, Note = request.Note, Lines = request.ToLines(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return this.Failure(response);

        var requested = (RequestedAdjustment)response.Result!;
        return StatusCode(201, new
        {
            adjustmentId, number = requested.Number, status = requested.Status, valueBase = InventoryViewFormat.Money(requested.ValueBase, requested.CurrencyCode),
            requiresApproval = requested.RequiresApproval
        });
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStockAdjustmentQuery
        { AdjustmentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{id:guid}/approve")]
    [RequireAction(Actions.Stock.ApproveAdjustment)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecideStockAdjustmentRequest? request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApproveStockAdjustmentCommand
        {
            AdjustmentId = id, BranchId = branchId, Note = request?.Note,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/reject")]
    [RequireAction(Actions.Stock.ApproveAdjustment)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecideStockAdjustmentRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RejectStockAdjustmentCommand
        {
            AdjustmentId = id, BranchId = branchId, Note = request.Note ?? string.Empty,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/withdraw")]
    [RequireAction(Actions.Stock.Adjust)]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new WithdrawStockAdjustmentCommand
        {
            AdjustmentId = id, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }
}
