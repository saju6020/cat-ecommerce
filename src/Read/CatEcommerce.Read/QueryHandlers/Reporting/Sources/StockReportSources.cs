using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

internal static class StockQueries
{
    public static IQueryable<RvStockLedgerRow> Ledger(CatEcommerceDbContext db, ReportRequest request)
    {
        var ledger = db.Set<RvStockLedgerRow>().AsNoTracking();
        return request.AllBranches ? ledger : ledger.Where(e => e.BranchId == request.BranchId);
    }

    public static async Task<Dictionary<Guid, RvBatchRow>> BatchesAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Set<RvBatchRow>().AsNoTracking().Where(b => ids.Contains(b.BatchId)).ToDictionaryAsync(b => b.BatchId, ct);

    public static async Task<Dictionary<Guid, RvSkuRow>> SkusAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Set<RvSkuRow>().AsNoTracking().Where(s => ids.Contains(s.SkuId)).ToDictionaryAsync(s => s.SkuId, ct);

    public static async Task<Dictionary<Guid, RvStockLocationRow>> LocationsAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Set<RvStockLocationRow>().AsNoTracking().Where(l => ids.Contains(l.LocationId)).ToDictionaryAsync(l => l.LocationId, ct);
}

/// <summary>R5 -- <c>stock-position</c> (US-RPT-02): the roll-forward of the ledger per batch and location, valued at landed cost where one exists.</summary>
public sealed class StockPositionSource : IReportSource
{
    public string Key => ReportCatalogue.StockPositionKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var start = request.StartInclusive;
        var end = request.EndExclusive;

        var aggregates = await StockQueries.Ledger(db, request).Where(e => e.OccurredOn < end)
            .GroupBy(e => new { e.BatchId, e.LocationId, e.MovementKind })
            .Select(g => new LedgerAggregate(
                g.Key.BatchId, g.Key.LocationId, g.Key.MovementKind,
                g.Where(e => e.OccurredOn < start).Sum(e => e.QuantityDelta), g.Where(e => e.OccurredOn >= start).Sum(e => e.QuantityDelta)))
            .ToListAsync(ct);

        var batchIds = aggregates.Select(a => a.BatchId).Distinct().ToList();
        var batches = await StockQueries.BatchesAsync(db, batchIds, ct);
        var skus = await StockQueries.SkusAsync(db, batches.Values.Select(b => b.SkuId).Distinct().ToList(), ct);
        var locations = await StockQueries.LocationsAsync(db, aggregates.Select(a => a.LocationId).Distinct().ToList(), ct);

        var output = StockPositionBuilder.Build(aggregates, batches, skus, locations, request.Flag("includeZero"), request.CurrencyCode);
        return new ReportSourceResult(output.Rows.Take(request.RowLimit).ToList(), output.Totals, output.Notes);
    }
}

/// <summary>R6 -- <c>stock-near-expiry</c> (US-RPT-02): batches with stock that expire in the window, or already have, with the value at risk.</summary>
public sealed class StockNearExpirySource : IReportSource
{
    public string Key => ReportCatalogue.StockNearExpiryKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var today = request.LocalDate(request.Now);
        var horizon = request.To >= today ? request.To : today.AddDays(-1);
        var includeExpired = request.Flag("includeExpired");

        // What is on hand now (the sum of the batch's ledger entries), for batches that expire inside the window or, when asked, have expired.
        var query = from e in StockQueries.Ledger(db, request)
                    join b in db.Set<RvBatchRow>().AsNoTracking() on e.BatchId equals b.BatchId
                    where b.ExpiryOn != null && (b.ExpiryOn <= horizon || (includeExpired && b.ExpiryOn < today))
                    group e by new { e.BatchId, e.LocationId } into g
                    select new OnHandLine(g.Key.BatchId, g.Key.LocationId, g.Sum(x => x.QuantityDelta));

        var onHand = (await query.ToListAsync(ct)).Where(l => l.Quantity > 0m).ToList();

        var batches = await StockQueries.BatchesAsync(db, onHand.Select(l => l.BatchId).Distinct().ToList(), ct);
        var skus = await StockQueries.SkusAsync(db, batches.Values.Select(b => b.SkuId).Distinct().ToList(), ct);
        var locations = await StockQueries.LocationsAsync(db, onHand.Select(l => l.LocationId).Distinct().ToList(), ct);

        var branchForSettings = request.BranchId;
        var thresholds = ExpiryAlertRules.Thresholds(
            request.Settings.GetInt(SettingKeys.InventoryExpiryAlertFirstDays, branchForSettings),
            request.Settings.GetInt(SettingKeys.InventoryExpiryAlertSecondDays, branchForSettings),
            request.Settings.GetInt(SettingKeys.InventoryExpiryAlertThirdDays, branchForSettings));

        var output = NearExpiryBuilder.Build(onHand, batches, skus, locations, request.From, request.To, today, includeExpired, thresholds, request.CurrencyCode);
        return new ReportSourceResult(output.Rows.Take(request.RowLimit).ToList(), output.Totals, output.Notes);
    }
}

/// <summary>
///  The inputs (positions, reorder policy, what is on order) come from the `report.rv_*` views; the usable stock
/// and the suggestion are <b>HO-IMP-001's own</b> pure services, <see cref="StockUsability"/> and <see cref="ReorderSuggestionCalculator"/>, called and not re-implemented (U-C4). Only the consumption window is the report's own: the range.
/// </summary>
public sealed class StockLowReorderSource : IReportSource
{
    private static readonly int[] Consuming = [MovementKind.Sale.Value, MovementKind.Dispense.Value, MovementKind.ClinicalUse.Value, MovementKind.BoardingUse.Value];

    public string Key => ReportCatalogue.StockLowReorderKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var calculator = new ReorderSuggestionCalculator();
        var today = request.LocalDate(request.Now);
        var windowDays = request.To.DayNumber - request.From.DayNumber + 1;

        var branchIds = request.AllBranches
            ? await db.Set<RvBranchRow>().AsNoTracking().Where(b => b.IsActive).Select(b => b.BranchId).ToListAsync(ct)
            : [request.BranchId!.Value];

        var rows = new List<ReportRow>();
        foreach (var branchId in branchIds)
        {
            var coverDays = request.Settings.GetInt(SettingKeys.InventoryReorderCoverDays, branchId);
            var defaultLead = request.Settings.GetInt(SettingKeys.InventoryDefaultLeadTimeDays);
            var defaultPoint = request.Settings.GetDecimal(SettingKeys.InventoryDefaultReorderPoint, branchId);

            // Every active location that is not the quarantine one; the pairs are the positions there plus every pair that has a policy (HO-IMP-001's own rule).
            var quarantine = StockLocationKind.Quarantine.Value;
            var locations = await db.Set<RvStockLocationRow>().AsNoTracking()
                .Where(l => l.BranchId == branchId && l.IsActive && l.Kind != quarantine).Select(l => new { l.LocationId, l.Code, l.Kind }).ToListAsync(ct);
            if (locations.Count == 0) continue;

            var locationIds = locations.Select(l => l.LocationId).ToList();
            var kindOf = locations.ToDictionary(l => l.LocationId, l => Enumeration.FromValue<StockLocationKind>(l.Kind));
            var codeOf = locations.ToDictionary(l => l.LocationId, l => l.Code);

            var positions = await db.Set<RvStockPositionRow>().AsNoTracking().Where(p => locationIds.Contains(p.LocationId)).ToListAsync(ct);
            var policies = await db.Set<RvReorderPolicyRow>().AsNoTracking().Where(p => locationIds.Contains(p.LocationId)).ToListAsync(ct);

            // HO-IMP-001's own usability rule, applied to the view's inputs.
            var usable = positions.GroupBy(p => (p.SkuId, p.LocationId)).ToDictionary(
                g => g.Key,
                g => g.Sum(p => StockUsability.Usable(p.OnHand, p.Reserved, Enumeration.FromValue<BatchStatus>(p.BatchStatus), kindOf[p.LocationId], p.ExpiryOn, today)));
            var names = positions.GroupBy(p => p.SkuId).ToDictionary(g => g.Key, g => g.First());
            var keys = usable.Keys.Concat(policies.Select(p => (p.SkuId, p.LocationId))).Distinct().ToList();

            var missing = keys.Where(k => !names.ContainsKey(k.SkuId)).Select(k => k.SkuId).Distinct().ToList();
            var extraNames = missing.Count == 0
                ? new Dictionary<Guid, (string SkuCode, string DisplayName)>()
                : (await db.Set<RvBatchRow>().AsNoTracking().Where(b => missing.Contains(b.SkuId)).Select(b => new { b.SkuId, b.SkuCode, b.DisplayName }).ToListAsync(ct))
                    .GroupBy(b => b.SkuId).ToDictionary(g => g.Key, g => (g.First().SkuCode, g.First().DisplayName));

            var consumption = (await db.Set<RvStockLedgerRow>().AsNoTracking()
                    .Where(e => e.BranchId == branchId && e.OccurredOn >= request.StartInclusive && e.OccurredOn < request.EndExclusive && Consuming.Contains(e.MovementKind))
                    .GroupBy(e => new { e.SkuId, e.LocationId }).Select(g => new { g.Key.SkuId, g.Key.LocationId, Quantity = g.Sum(e => -e.QuantityDelta) }).ToListAsync(ct))
                .ToDictionary(r => (r.SkuId, r.LocationId), r => r.Quantity);
            var onOrder = (await db.Set<RvOnOrderRow>().AsNoTracking().Where(o => o.BranchId == branchId).ToListAsync(ct)).ToDictionary(o => (o.SkuId, o.LocationId), o => o.Outstanding);

            foreach (var key in keys)
            {
                var policy = policies.FirstOrDefault(p => p.SkuId == key.SkuId && p.LocationId == key.LocationId);
                var (skuCode, display) = names.TryGetValue(key.SkuId, out var n) ? (n.SkuCode, n.DisplayName) : extraNames.GetValueOrDefault(key.SkuId);
                var point = policy?.ReorderPoint ?? defaultPoint;
                var onHandUsable = usable.GetValueOrDefault(key);
                var lead = policy?.LeadTimeDays ?? defaultLead;

                var suggestion = calculator.Suggest(new ReorderInput(
                    consumption.GetValueOrDefault(key), windowDays, lead, coverDays, point, onHandUsable, onOrder.GetValueOrDefault(key), policy?.ReorderQuantity));

                // The same two filters HO-IMP-001's own suggestion list applies.
                if (suggestion.SuggestedQuantity <= 0m && onHandUsable >= point) continue;
                if (point <= 0m && policy?.ReorderQuantity is null && suggestion.SuggestedQuantity <= 0m) continue;

                rows.Add(new ReportRow
                {
                    ["skuCode"] = skuCode,
                    ["productName"] = display,
                    ["locationName"] = codeOf[key.LocationId],
                    ["onHand"] = onHandUsable,
                    ["reorderPoint"] = point,
                    ["averageDailyConsumption"] = decimal.Round(suggestion.AverageDailyConsumption, 4),
                    ["supplierName"] = policy?.SupplierName,
                    ["leadTimeDays"] = (long)lead,
                    ["suggestedQuantity"] = suggestion.SuggestedQuantity,
                });
            }
        }

        return new ReportSourceResult(rows.OrderBy(r => (string?)r["productName"], StringComparer.OrdinalIgnoreCase).ThenBy(r => (string?)r["locationName"], StringComparer.Ordinal).Take(request.RowLimit).ToList());
    }
}

/// <summary>R8 -- <c>import-consignments</c> (SRS section 8.1): the consignments created in the range, with their clearance documents.</summary>
public sealed class ImportConsignmentsSource : IReportSource
{
    public string Key => ReportCatalogue.ImportConsignmentsKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var query = db.Set<RvConsignmentRow>().AsNoTracking().Where(c => c.CreatedOn >= request.StartInclusive && c.CreatedOn < request.EndExclusive);
        if (!request.AllBranches) query = query.Where(c => c.BranchId == request.BranchId);

        var consignments = await query.OrderByDescending(c => c.CreatedOn).Take(request.RowLimit).ToListAsync(ct);

        var rows = consignments.Select(c => new ReportRow
        {
            ["reference"] = c.Reference,
            ["shipmentReference"] = c.ShipmentReference,
            ["originCountry"] = c.OriginCountry.Trim(),
            ["supplierName"] = c.SupplierName,
            ["poCount"] = (long)c.PoCount,
            ["clearanceStatus"] = ReportLabels.Of<ConsignmentStatus>(c.ClearanceStatus),
            ["landedCostTotal"] = c.LandedCostTotal is { } total ? new ReportMoney(ReportMath.Money(total), request.CurrencyCode) : null,
            ["hasPermit"] = c.HasPermit,
            ["hasHealthCertificate"] = c.HasHealthCertificate,
            ["hasCustomsDeclaration"] = c.HasCustomsDeclaration,
            ["batchesReceived"] = (long)c.BatchesReceived,
            ["createdOn"] = c.CreatedOn,
            ["clearedOn"] = c.ClearedOn,
        }).ToList<ReportRow>();

        var notes = new List<ReportNote>();
        if (consignments.Any(c => c.LandedCostTotal is null))
            notes.Add(new ReportNote("LandedCostPending", "A landed cost is shown once the consignment's costs have been allocated across its batches; until then it is not estimated."));

        return new ReportSourceResult(rows, null, notes);
    }
}
