using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 (US-IMP-10 AC2, SRS §8.1) -- the inventory reports of the ACTIVE branch. They carry costs, so they need
/// <c>InventoryReports.View</c>. The CSV forms use <see cref="InventoryCsv"/> (ADR-051's injection guard).</summary>
[ApiController]
[Route("api/staff/inventory/reports")]
public sealed class StaffInventoryReportsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    private static readonly IReadOnlyList<string> NearExpiryHeaders =
        ["SkuCode", "Product", "Lot", "ExpiresOn", "DaysRemaining", "Expired", "Status", "Quantity", "UnitCostBase", "CostBasis", "ValueAtRisk", "Currency"];

    private static readonly IReadOnlyList<string> StockOnHandHeaders =
        ["SkuCode", "Product", "Class", "Lot", "ExpiresOn", "BatchStatus", "Location", "OnHand", "Reserved", "UnitCostBase", "CostBasis", "Value", "Currency"];

    private static int MaxWithinDays => 3650;

    [HttpGet("near-expiry")]
    [RequireAction(Actions.InventoryReports.View)]
    public async Task<IActionResult> NearExpiry([FromQuery] int? withinDays, [FromQuery] bool includeExpired)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;
        if (withinDays is < 0 or > 3650) return this.InvalidQuery("withinDays", $"withinDays must be between 0 and {MaxWithinDays}.");

        var response = await dispatcher.GetResultAsync(NearExpiryQuery(branchId, withinDays, includeExpired));
        return Ok(response.Result);
    }

    [HttpGet("near-expiry.csv")]
    [RequireAction(Actions.InventoryReports.View)]
    public async Task<IActionResult> NearExpiryCsv([FromQuery] int? withinDays, [FromQuery] bool includeExpired)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;
        if (withinDays is < 0 or > 3650) return this.InvalidQuery("withinDays", $"withinDays must be between 0 and {MaxWithinDays}.");

        var report = (await dispatcher.GetResultAsync(NearExpiryQuery(branchId, withinDays, includeExpired))).Result!;
        var csv = InventoryCsv.Build(NearExpiryHeaders, report.Rows.Select(r => (IReadOnlyList<object?>)new object?[]
        {
            r.SkuCode, r.DisplayName, r.LotNumber, r.ExpiresOn, r.DaysRemaining, r.IsExpired, r.Status, r.Quantity, r.UnitCostBase.Amount, r.CostBasis, r.ValueAtRisk.Amount, r.ValueAtRisk.CurrencyCode
        }));

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "near-expiry.csv");
    }

    [HttpGet("stock-on-hand")]
    [RequireAction(Actions.InventoryReports.View)]
    public async Task<IActionResult> StockOnHand([FromQuery] Guid? locationId, [FromQuery(Name = "class")] string? stockClass)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        return Ok((await dispatcher.GetResultAsync(StockOnHandQuery(branchId, locationId, stockClass))).Result);
    }

    [HttpGet("stock-on-hand.csv")]
    [RequireAction(Actions.InventoryReports.View)]
    public async Task<IActionResult> StockOnHandCsv([FromQuery] Guid? locationId, [FromQuery(Name = "class")] string? stockClass)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var report = (await dispatcher.GetResultAsync(StockOnHandQuery(branchId, locationId, stockClass))).Result!;
        var csv = InventoryCsv.Build(StockOnHandHeaders, report.Rows.Select(r => (IReadOnlyList<object?>)new object?[]
        {
            r.SkuCode, r.DisplayName, r.StockClass, r.LotNumber, r.ExpiresOn, r.BatchStatus, r.LocationCode, r.OnHand, r.Reserved, r.UnitCostBase.Amount, r.CostBasis, r.Value.Amount, r.Value.CurrencyCode
        }));

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "stock-on-hand.csv");
    }

    /// <summary>§8.13 -- positions that differ from the entries they are derived from; expected empty, always.</summary>
    [HttpGet("reconciliation")]
    [RequireAction(Actions.InventoryReports.View)]
    public async Task<IActionResult> Reconciliation()
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStockReconciliationQuery
        { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(new { items = response.Result });
    }

    private GetNearExpiryReportQuery NearExpiryQuery(Guid branchId, int? withinDays, bool includeExpired) => new()
    {
        BranchId = branchId, WithinDays = withinDays, IncludeExpired = includeExpired, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
    };

    private GetStockOnHandReportQuery StockOnHandQuery(Guid branchId, Guid? locationId, string? stockClass) => new()
    {
        BranchId = branchId, LocationId = locationId, Class = stockClass, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
    };
}
