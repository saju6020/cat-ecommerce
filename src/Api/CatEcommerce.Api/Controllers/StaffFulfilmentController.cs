using System.Globalization;
using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 (US-ORD-10, US-ORD-11 AC2) -- the shop-floor reads of fulfilment: the pick list, the courier list a dispatch chooses from and the CSV manifest.
/// Branch-scoped to the staff member's active branch. The pick list is <c>Orders.View</c> (Inventory staff read it, Appendix D); the manifest and the courier
/// list are <c>Orders.Fulfil</c>.
/// </summary>
[ApiController]
[Route("api/staff/fulfilment")]
public sealed class StaffFulfilmentController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffOrderControllerBase(activeBranchAccessor)
{
    private static readonly string[] ManifestHeaders =
        ["Order number", "Recipient", "Phone", "Address", "District", "Postal code", "Parcels", "Weight (g)", "COD amount", "Tracking number"];

    /// <summary>US-ORD-10 AC1 -- grouped by location, then batch, quantities per order. <c>orderIds</c> is a comma-separated list; without it, every confirmed and
    /// packing order of the branch.</summary>
    [HttpGet("pick-list")]
    [RequireAction(Actions.Orders.View)]
    public async Task<IActionResult> PickList([FromQuery] string? orderIds)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        List<Guid>? ids = null;
        if (!string.IsNullOrWhiteSpace(orderIds))
        {
            ids = [];
            foreach (var part in orderIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var id)) return BadRequest(new { error = "orderIds must be a comma-separated list of ids." });
                ids.Add(id);
            }

            if (ids.Count > 200) return BadRequest(new { error = "At most 200 orders at a time." });
        }

        var response = await dispatcher.GetResultAsync(new GetPickListQuery { BranchId = branchId, OrderIds = ids, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    /// <summary>The active couriers a dispatch chooses from (the admin list is <c>Couriers.Manage</c> only).</summary>
    [HttpGet("couriers")]
    [RequireAction(Actions.Orders.Fulfil)]
    public async Task<IActionResult> Couriers()
    {
        var response = await dispatcher.GetResultAsync(new GetCouriersQuery { ActiveOnly = true, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    /// <summary>US-ORD-11 AC2 -- the courier manifest as <c>text/csv</c>: one row per order dispatched with that courier on that branch-local date, with the COD
    /// amount to collect (0 when prepaid). A text cell beginning <c>= + - @</c> is prefixed with <c>'</c> (HO-ADM-001 ADR-051).</summary>
    [HttpGet("manifest.csv")]
    [RequireAction(Actions.Orders.Fulfil)]
    public async Task<IActionResult> Manifest([FromQuery] Guid courierId, [FromQuery] string? date)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (courierId == Guid.Empty) return BadRequest(new { error = "courierId is required." });
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return BadRequest(new { error = "date must be yyyy-MM-dd." });

        var response = await dispatcher.GetResultAsync(new GetCourierManifestQuery
        { BranchId = branchId, CourierId = courierId, Date = parsed, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        var rows = (response.Result ?? []).Select(r => (IReadOnlyList<object?>)new object?[]
            { r.OrderNumber, r.RecipientName, r.Phone, r.Address, r.District, r.PostalCode, r.Parcels, r.WeightGrams, r.CodAmount.ToString("0.00", CultureInfo.InvariantCulture), r.TrackingNumber });

        return File(Encoding.UTF8.GetBytes(InventoryCsv.Build(ManifestHeaders, rows)), "text/csv", $"manifest-{parsed:yyyy-MM-dd}.csv");
    }
}
