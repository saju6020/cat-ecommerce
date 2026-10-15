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

/// <summary>HO-IMP-001 §12.4 (US-IMP-02) -- purchase orders of the ACTIVE branch; another branch's order is a 404. Receipt progress is derived
/// from posted receipts (ADR-112).</summary>
[ApiController]
[Route("api/staff/procurement/purchase-orders")]
public sealed class ProcurementPurchaseOrdersController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] Guid? supplierId, [FromQuery] string? receiptState, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetPurchaseOrdersQuery
        {
            BranchId = branchId, Status = status, SupplierId = supplierId, ReceiptState = receiptState, From = from, To = to,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Create([FromBody] PurchaseOrderRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var purchaseOrderId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreatePurchaseOrderCommand
        {
            PurchaseOrderId = purchaseOrderId, BranchId = branchId, SupplierId = request.SupplierId, DeliverToLocationId = request.DeliverToLocationId,
            CurrencyCode = request.CurrencyCode, ExpectedDeliveryOn = request.ExpectedDeliveryOn, Notes = request.Notes, Lines = ToLines(request),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.CreatedOrFailure(response, new { purchaseOrderId, number = response.Result as string });
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPurchaseOrderQuery
        { PurchaseOrderId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{id:guid}")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> ReplaceDraft(Guid id, [FromBody] PurchaseOrderRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReplacePurchaseOrderDraftCommand
        {
            PurchaseOrderId = id, BranchId = branchId, SupplierId = request.SupplierId, DeliverToLocationId = request.DeliverToLocationId,
            CurrencyCode = request.CurrencyCode, ExpectedDeliveryOn = request.ExpectedDeliveryOn, Notes = request.Notes, Lines = ToLines(request),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/issue")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Issue(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new IssuePurchaseOrderCommand
        {
            PurchaseOrderId = id, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] PurchaseOrderReasonRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CancelPurchaseOrderCommand
        {
            PurchaseOrderId = id, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/close")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Close(Guid id, [FromBody] PurchaseOrderReasonRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ClosePurchaseOrderCommand
        {
            PurchaseOrderId = id, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    /// <summary>Every line id is minted here, never taken from the client (§11.1).</summary>
    private static IReadOnlyList<PurchaseOrderLineInput> ToLines(PurchaseOrderRequest request) =>
        request.Lines.Select(l => new PurchaseOrderLineInput(Guid.NewGuid(), l.SkuId, l.OrderedQuantity, l.UnitCostAmount)).ToList();
}
