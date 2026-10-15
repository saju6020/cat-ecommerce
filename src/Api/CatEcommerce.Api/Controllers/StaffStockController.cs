using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 (US-IMP-06, US-IMP-07) -- stock per SKU and the batch x location matrix of the ACTIVE branch. Every figure is DERIVED
/// from the ledger's projection; there is no editable stock number anywhere (§4.4).</summary>
[ApiController]
[Route("api/staff/inventory/stock")]
public sealed class StaffStockController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Summary(
        [FromQuery] string? search, [FromQuery] Guid? locationId, [FromQuery(Name = "class")] string? stockClass, [FromQuery] bool belowReorderPoint,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetStockSummaryQuery
        {
            BranchId = branchId, Search = search, LocationId = locationId, Class = stockClass, BelowReorderPoint = belowReorderPoint,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("{skuId:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Matrix(Guid skuId)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetSkuStockMatrixQuery
        { BranchId = branchId, SkuId = skuId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>US-IMP-07 AC1 -- what FEFO would allocate for a demand, without allocating (<c>IStockLedger.PlanAsync</c>, so the preview cannot disagree with
    /// the real thing). Answers the plan, with <c>shortfall</c> when stock does not cover it; a refusal (unknown SKU, a purpose the location does not permit)
    /// is a 400.</summary>
    [HttpGet("~/api/staff/inventory/fefo-preview")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> FefoPreview([FromQuery] Guid skuId, [FromQuery] decimal quantity, [FromQuery] string purpose, [FromQuery] Guid? locationId)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;
        if (skuId == Guid.Empty) return this.InvalidQuery("skuId", "skuId is required.");
        if (quantity <= 0m) return this.InvalidQuery("quantity", "quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(purpose)) return this.InvalidQuery("purpose", "purpose is required.");

        var response = await dispatcher.GetResultAsync(new GetFefoPreviewQuery
        {
            BranchId = branchId, SkuId = skuId, Quantity = quantity, Purpose = purpose, LocationId = locationId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        var preview = response.Result!;
        return preview.RefusalCode is null
            ? Ok(new { lines = preview.Lines, shortfall = preview.Shortfall, stockUnit = preview.StockUnit })
            : this.InvalidQuery(preview.RefusalCode, preview.RefusalMessage!);
    }
}
