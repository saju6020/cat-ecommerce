using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 (US-IMP-04) -- goods receipts of the ACTIVE branch. A receipt is posted in one command (ADR-105) and is immutable
/// afterwards; a mistake is corrected by an adjustment. A failure returns EVERY line violation, not just the first.</summary>
[ApiController]
[Route("api/staff/inventory/goods-receipts")]
public sealed class StaffGoodsReceiptsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? supplierId, [FromQuery] Guid? purchaseOrderId,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetGoodsReceiptsQuery
        {
            BranchId = branchId, From = from, To = to, SupplierId = supplierId, PurchaseOrderId = purchaseOrderId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Stock.Receive)]
    public async Task<IActionResult> Post([FromBody] PostGoodsReceiptRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var goodsReceiptId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new PostGoodsReceiptCommand
        {
            GoodsReceiptId = goodsReceiptId, BranchId = branchId, SupplierId = request.SupplierId, PurchaseOrderId = request.PurchaseOrderId,
            ConsignmentId = request.ConsignmentId, DeliveryReference = request.DeliveryReference, Lines = request.ToLines(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return this.Failure(response);

        var posted = (PostedGoodsReceipt)response.Result!;
        return StatusCode(201, new { goodsReceiptId, number = posted.Number, batchIds = posted.BatchIds });
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetGoodsReceiptQuery
        {
            GoodsReceiptId = id, BranchId = branchId, IncludeCosts = user.Can(Actions.InventoryReports.View),
            UserContext = user, CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
