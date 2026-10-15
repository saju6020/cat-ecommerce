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

/// <summary>HO-IMP-001 §12.4 (US-IMP-06, US-IMP-09) -- batches of the ACTIVE branch. "Expired" is DERIVED in the branch's local date (ADR-101) and is
/// never a status you can set: the only transitions here are quarantine, release and write-off (the recall is <c>StaffRecallsController</c>). A
/// batch's costs are returned only to a caller who may see valuation (<c>InventoryReports.View</c>).</summary>
[ApiController]
[Route("api/staff/inventory/batches")]
public sealed class StaffBatchesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] Guid? skuId, [FromQuery] string? status, [FromQuery] int? expiringWithinDays, [FromQuery] Guid? consignmentId,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;
        if (expiringWithinDays is < 0 or > 3650) return this.InvalidQuery("expiringWithinDays", "expiringWithinDays must be between 0 and 3650.");

        var query = new GetBatchesQuery
        {
            BranchId = branchId, Search = search, SkuId = skuId, Status = status, ExpiringWithinDays = expiringWithinDays, ConsignmentId = consignmentId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("{batchId:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Get(Guid batchId)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetBatchQuery
        { BatchId = batchId, BranchId = branchId, IncludeCosts = user.Can(Actions.InventoryReports.View), UserContext = user, CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{batchId:guid}/quarantine")]
    [RequireAction(Actions.Batches.ManageStatus)]
    public async Task<IActionResult> Quarantine(Guid batchId, [FromBody] BatchReasonRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new QuarantineBatchCommand
        {
            BatchId = batchId, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{batchId:guid}/release")]
    [RequireAction(Actions.Batches.ManageStatus)]
    public async Task<IActionResult> Release(Guid batchId)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReleaseBatchCommand
        {
            BatchId = batchId, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{batchId:guid}/write-off")]
    [RequireAction(Actions.Batches.ManageStatus)]
    public async Task<IActionResult> WriteOff(Guid batchId, [FromBody] BatchReasonRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new WriteOffBatchCommand
        {
            BatchId = batchId, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }
}
