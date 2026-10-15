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

/// <summary>HO-IMP-001 §12.4 (US-IMP-15) -- the immutable stock ledger of the ACTIVE branch. <c>from</c> and <c>to</c> are REQUIRED and span at most
/// <see cref="InventoryRules.LedgerQueryMaxRangeDays"/> days, so a search can never become a full-table scan. Rows carry SKU, batch, delta, location,
/// reference and user -- and NO customer or cat (those are in the recall trace only, ADR-104).</summary>
[ApiController]
[Route("api/staff/inventory/ledger")]
public sealed class StaffLedgerController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Search(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? skuId, [FromQuery] Guid? batchId, [FromQuery] Guid? locationId,
        [FromQuery] string? kind, [FromQuery] string? referenceKind, [FromQuery] Guid? referenceId, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        if (from is null || to is null) return this.InvalidQuery("from", "from and to are required.");
        if (to < from) return this.InvalidQuery("to", "to cannot be before from.");
        if (to.Value.DayNumber - from.Value.DayNumber + 1 > InventoryRules.LedgerQueryMaxRangeDays)
            return this.InvalidQuery("to", $"The range cannot exceed {InventoryRules.LedgerQueryMaxRangeDays} days.");

        var query = new GetLedgerQuery
        {
            BranchId = branchId, From = from.Value, To = to.Value, SkuId = skuId, BatchId = batchId, LocationId = locationId, Kind = kind,
            ReferenceKind = referenceKind, ReferenceId = referenceId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    /// <summary>US-IMP-15 -- the same search as a CSV file (injection-guarded), at most <see cref="InventoryRules.LedgerCsvMaxRows"/> rows: a bigger result is a
    /// <c>413</c> asking for a narrower range. Needs <c>InventoryReports.View</c>.</summary>
    [HttpGet("export.csv")]
    [RequireAction(Actions.InventoryReports.View)]
    public async Task<IActionResult> Export(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? skuId, [FromQuery] Guid? batchId, [FromQuery] Guid? locationId,
        [FromQuery] string? kind, [FromQuery] string? referenceKind, [FromQuery] Guid? referenceId)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        if (from is null || to is null) return this.InvalidQuery("from", "from and to are required.");
        if (to < from) return this.InvalidQuery("to", "to cannot be before from.");
        if (to.Value.DayNumber - from.Value.DayNumber + 1 > InventoryRules.LedgerQueryMaxRangeDays)
            return this.InvalidQuery("to", $"The range cannot exceed {InventoryRules.LedgerQueryMaxRangeDays} days.");

        var query = new GetLedgerQuery
        {
            BranchId = branchId, From = from.Value, To = to.Value, SkuId = skuId, BatchId = batchId, LocationId = locationId, Kind = kind,
            ReferenceKind = referenceKind, ReferenceId = referenceId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
            PageNumber = 1, PageSize = InventoryRules.LedgerCsvMaxRows + 1
        };

        var page = (await dispatcher.GetResultAsync(query)).Result!;
        if (page.TotalCount > InventoryRules.LedgerCsvMaxRows)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = $"The export is limited to {InventoryRules.LedgerCsvMaxRows} rows; narrow the date range or add a filter." });

        var csv = InventoryCsv.Build(
            ["Sequence", "OccurredOn", "Kind", "QuantityDelta", "Unit", "Location", "SkuCode", "Lot", "ReferenceKind", "Reference", "FefoOverridden", "OverrideReason", "PerformedBy", "Note"],
            page.Items.Select(e => (IReadOnlyList<object?>)new object?[]
            {
                e.Sequence, e.OccurredOn, e.Kind, e.QuantityDelta, e.StockUnit, e.LocationCode, e.SkuCode, e.LotNumber, e.ReferenceKind, e.ReferenceLabel, e.FefoOverridden,
                e.OverrideReason, e.PerformedByName, e.Note
            }));

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "stock-ledger.csv");
    }
}
