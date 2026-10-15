using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>
/// HO-RPT-001 section 8.3 R27 (SRS section 8.1, OQ-18), <b>pure</b>. <b>Age</b> is the whole hours since the order was placed, to its dispatch (or to now while it is still open). The dispatch service level runs from
/// <b>confirmation</b> -- an order awaiting its bank transfer cannot be dispatched, so it is not late -- to dispatch (or to now while waiting): more than the SLA is a breach. Cancelled orders never breach.
/// </summary>
public static class OrderFulfilmentBuilder
{
    public static bool Breached(RvOrderRow order, DateTimeOffset now, int slaHours)
    {
        if (order.Status == OrderStatus.Cancelled.Value || order.ConfirmedOn is not { } confirmed) return false;

        var end = order.DispatchedOn ?? now;
        return (end - confirmed).TotalHours > slaHours;
    }

    public static IReadOnlyList<ReportRow> Build(IEnumerable<RvOrderRow> orders, DateTimeOffset now, int slaHours) =>
        orders.OrderByDescending(o => o.PlacedOn).Select(o =>
        {
            var end = o.DispatchedOn ?? o.CancelledOn ?? now;
            return new ReportRow
            {
                ["orderNumber"] = o.OrderNumber,
                ["placedOn"] = o.PlacedOn,
                ["channel"] = ReportLabels.Of<OrderChannel>(o.Channel),
                ["status"] = ReportLabels.Of<OrderStatus>(o.Status),
                ["ageHours"] = (long)Math.Floor((end - o.PlacedOn).TotalHours),
                ["slaBreach"] = Breached(o, now, slaHours),
                ["dispatchedOn"] = o.DispatchedOn,
                ["deliveredOn"] = o.DeliveredOn,
            };
        }).ToList<ReportRow>();
}

/// <summary>R27 -- <c>order-fulfilment</c> (SRS section 8.1).</summary>
public sealed class OrderFulfilmentSource : IReportSource
{
    public string Key => ReportCatalogue.OrderFulfilmentKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var query = db.Set<RvOrderRow>().AsNoTracking().Where(o => o.PlacedOn >= request.StartInclusive && o.PlacedOn < request.EndExclusive);
        if (!request.AllBranches) query = query.Where(o => o.BranchId == request.BranchId);

        var orders = await query.OrderByDescending(o => o.PlacedOn).Take(request.RowLimit).ToListAsync(ct);
        var rows = OrderFulfilmentBuilder.Build(orders, request.Now, FulfilmentRules.DispatchSlaHours);

        var totals = new ReportRow { ["orderNumber"] = $"{rows.Count} orders", ["ageHours"] = rows.Count(r => (bool)r["slaBreach"]!) };
        var notes = new List<ReportNote>
        {
            new("DispatchSla", $"The dispatch service level is {FulfilmentRules.DispatchSlaHours} hours from confirmation (HO-ORD-001 defines no setting for it). The total under Age is the number of orders that breached it."),
        };
        return new ReportSourceResult(rows, totals, notes);
    }
}
