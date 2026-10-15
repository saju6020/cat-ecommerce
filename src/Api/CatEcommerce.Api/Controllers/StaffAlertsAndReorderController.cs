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

/// <summary>HO-IMP-001 §12.4 (US-IMP-10, US-IMP-12) -- the alerts of the ACTIVE branch. They are raised by the worker, never over HTTP; staff can only list,
/// count and acknowledge them.</summary>
[ApiController]
[Route("api/staff/inventory/alerts")]
public sealed class StaffAlertsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? kind, [FromQuery] Guid? locationId, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetStockAlertsQuery
        {
            BranchId = branchId, Status = status, Kind = kind, LocationId = locationId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    /// <summary>The navigation badge: <c>{ open: { total, byKind } }</c>.</summary>
    [HttpGet("summary")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Summary()
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStockAlertSummaryQuery
        { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [RequireAction(Actions.StockAlerts.Acknowledge)]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeStockAlertRequest? request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new AcknowledgeStockAlertCommand
        {
            AlertId = id, BranchId = branchId, Note = request?.Note,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }
}

/// <summary>HO-IMP-001 §12.4 (US-IMP-12) -- reorder policies per (SKU, location) of the ACTIVE branch, and the suggestions computed from them.</summary>
[ApiController]
public sealed class StaffReorderPoliciesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    /// <summary>Every pair with stock or a policy, with its EFFECTIVE point and whether that point is the default.</summary>
    [HttpGet("api/staff/inventory/reorder-policies")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List([FromQuery] Guid? locationId, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetReorderPoliciesQuery
        { BranchId = branchId, LocationId = locationId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    /// <summary>An upsert. A reorder point of 0 silences the pair.</summary>
    [HttpPut("api/staff/inventory/reorder-policies/{skuId:guid}/{locationId:guid}")]
    [RequireAction(Actions.ReorderPolicies.Manage)]
    public async Task<IActionResult> Set(Guid skuId, Guid locationId, [FromBody] SetReorderPolicyRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SetReorderPolicyCommand
        {
            BranchId = branchId, SkuId = skuId, LocationId = locationId, ReorderPoint = request.ReorderPoint, ReorderQuantity = request.ReorderQuantity,
            PreferredSupplierId = request.PreferredSupplierId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    /// <summary>US-IMP-12 AC2 -- the suggested quantity per pair that needs stock.</summary>
    [HttpGet("api/staff/procurement/reorder-suggestions")]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> Suggestions([FromQuery] Guid? locationId, [FromQuery] Guid? supplierId)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetReorderSuggestionsQuery
        {
            BranchId = branchId, LocationId = locationId, SupplierId = supplierId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(new { items = response.Result });
    }
}
