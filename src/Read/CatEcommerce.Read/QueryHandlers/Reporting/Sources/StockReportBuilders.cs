using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>One (batch, location, movement kind) group of ledger entries: the sum before the range started and the sum inside it. The ledger is immutable (D-3), so these sums are reproducible.</summary>
public sealed record LedgerAggregate(Guid BatchId, Guid LocationId, int MovementKind, decimal Before, decimal During);

/// <summary>What a stock report returns beyond rows: totals and notes.</summary>
public sealed record StockReportOutput(IReadOnlyList<ReportRow> Rows, ReportRow Totals, IReadOnlyList<ReportNote> Notes);

/// <summary>
/// HO-RPT-001 section 8.3 R5 -- the roll-forward of the immutable stock ledger per batch and location, <b>pure</b>. <c>closing = opening + in - out +/- adjustments</c>. A transfer is an
/// <c>out</c> at one location and an <c>in</c> at the other. Valuation is the closing quantity at the batch's <b>landed</b> unit cost, or <c>null</c> when none exists -- there is no fallback to the
/// purchase-order cost (ADR-284, OQ-4); the totals row excludes the nulls and a note states how much of the stock the valuation covers.
/// </summary>
public static class StockPositionBuilder
{
    private static readonly int[] InKinds = [MovementKind.Receipt.Value, MovementKind.TransferIn.Value, MovementKind.Return.Value];
    private static readonly int[] OutKinds =
        [MovementKind.Sale.Value, MovementKind.Dispense.Value, MovementKind.ClinicalUse.Value, MovementKind.BoardingUse.Value, MovementKind.TransferOut.Value, MovementKind.Wastage.Value];
    private static readonly int[] AdjustKinds = [MovementKind.AdjustmentIn.Value, MovementKind.AdjustmentOut.Value, MovementKind.CountGain.Value, MovementKind.CountLoss.Value];

    public static StockReportOutput Build(
        IEnumerable<LedgerAggregate> ledger, IReadOnlyDictionary<Guid, RvBatchRow> batches, IReadOnlyDictionary<Guid, RvSkuRow> skus,
        IReadOnlyDictionary<Guid, RvStockLocationRow> locations, bool includeZero, string currencyCode)
    {
        var lines = new List<(RvBatchRow Batch, RvSkuRow? Sku, RvStockLocationRow? Location, decimal Opening, decimal In, decimal Out, decimal Adjust, decimal Closing)>();

        foreach (var group in ledger.GroupBy(l => (l.BatchId, l.LocationId)))
        {
            if (!batches.TryGetValue(group.Key.BatchId, out var batch)) continue;

            var opening = group.Sum(l => l.Before);
            var received = group.Where(l => InKinds.Contains(l.MovementKind)).Sum(l => l.During);
            var issued = -group.Where(l => OutKinds.Contains(l.MovementKind)).Sum(l => l.During);
            var adjusted = group.Where(l => AdjustKinds.Contains(l.MovementKind)).Sum(l => l.During);

            // A kind this report does not know still moves stock: it is counted by sign so the roll-forward always balances.
            foreach (var other in group.Where(l => !InKinds.Contains(l.MovementKind) && !OutKinds.Contains(l.MovementKind) && !AdjustKinds.Contains(l.MovementKind)))
            {
                if (other.During >= 0m) received += other.During;
                else issued -= other.During;
            }

            var closing = opening + received - issued + adjusted;
            if (!includeZero && opening == 0m && received == 0m && issued == 0m && adjusted == 0m && closing == 0m) continue;

            lines.Add((batch, skus.GetValueOrDefault(batch.SkuId), locations.GetValueOrDefault(group.Key.LocationId), opening, received, issued, adjusted, closing));
        }

        var ordered = lines.OrderBy(l => l.Sku?.ProductName ?? l.Batch.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Batch.LotNumber, StringComparer.Ordinal).ThenBy(l => l.Location?.Code, StringComparer.Ordinal).ToList();

        var rows = ordered.Select(l =>
        {
            ReportMoney? cost = l.Batch.LandedUnitCost is { } unit ? new ReportMoney(unit, currencyCode) : null;
            ReportMoney? valuation = l.Batch.LandedUnitCost is { } c ? new ReportMoney(ReportMath.Money(l.Closing * c), currencyCode) : null;

            return new ReportRow
            {
                ["skuCode"] = l.Sku?.SkuCode ?? l.Batch.SkuCode,
                ["productName"] = l.Sku?.ProductName ?? l.Batch.DisplayName,
                ["category"] = l.Sku is null ? null : ReportLabels.Of<ProductCategory>(l.Sku.Category),
                ["lotNumber"] = l.Batch.LotNumber,
                ["expiryOn"] = l.Batch.ExpiryOn,
                ["locationName"] = l.Location?.Name,
                ["batchStatus"] = ReportLabels.Of<BatchStatus>(l.Batch.Status),
                ["openingQuantity"] = l.Opening,
                ["receivedQuantity"] = l.In,
                ["issuedQuantity"] = l.Out,
                ["adjustedQuantity"] = l.Adjust,
                ["closingQuantity"] = l.Closing,
                ["landedUnitCost"] = cost,
                ["valuation"] = valuation,
            };
        }).ToList<ReportRow>();

        var stocked = ordered.Where(l => l.Closing > 0m).ToList();
        var costed = stocked.Count(l => l.Batch.LandedUnitCost is not null);
        var valuationRows = ordered.Where(l => l.Batch.LandedUnitCost is not null).ToList();

        var totals = new ReportRow { ["skuCode"] = $"{rows.Count} lines" };
        if (valuationRows.Count > 0) totals["valuation"] = new ReportMoney(valuationRows.Sum(l => ReportMath.Money(l.Closing * l.Batch.LandedUnitCost!.Value)), currencyCode);

        var notes = new List<ReportNote>();
        if (stocked.Count > 0 && costed < stocked.Count)
        {
            notes.Add(new ReportNote("ValuationCoverage", costed == 0
                ? "None of the stocked batches has a landed cost yet, so the stock valuation is not available."
                : $"Valuation covers {costed} of {stocked.Count} stocked batch lines ({ReportMath.Percent(costed, stocked.Count).ToString("0.0", CultureInfo.InvariantCulture)} %). " +
                  $"{stocked.Count - costed} came from batches with no landed cost yet."));
        }

        return new StockReportOutput(rows, totals, notes);
    }
}

/// <summary>What is on hand of one batch at one location right now: the sum of its ledger entries.</summary>
public sealed record OnHandLine(Guid BatchId, Guid LocationId, decimal Quantity);

/// <summary>
/// HO-RPT-001 section 8.3 R6 (US-RPT-02) -- near-expiry and expired stock with the value at risk, <b>pure</b>. The band comes from the three thresholds HO-IMP-001 configures
/// (<see cref="Shared.Common.Helpers.ExpiryAlertRules"/>, default 90 / 60 / 30): a batch is in the tightest band it has crossed. Value at risk is the quantity at landed cost (null when none) and at
/// the SKU's <b>current</b> retail price (OQ-3: both columns; the totals and the dashboard use cost).
/// </summary>
public static class NearExpiryBuilder
{
    public static StockReportOutput Build(
        IEnumerable<OnHandLine> onHand, IReadOnlyDictionary<Guid, RvBatchRow> batches, IReadOnlyDictionary<Guid, RvSkuRow> skus,
        IReadOnlyDictionary<Guid, RvStockLocationRow> locations, DateOnly from, DateOnly to, DateOnly today, bool includeExpired, IReadOnlyList<int> thresholds, string currencyCode)
    {
        var lines = new List<(RvBatchRow Batch, RvSkuRow? Sku, RvStockLocationRow? Location, decimal Quantity, int Days, ExpiryBand? Band, int? Threshold)>();

        foreach (var stock in onHand.Where(s => s.Quantity > 0m))
        {
            if (!batches.TryGetValue(stock.BatchId, out var batch) || batch.ExpiryOn is not { } expiry) continue;

            var days = expiry.DayNumber - today.DayNumber;
            var expired = days < 0;
            var include = expired ? includeExpired : expiry >= from && expiry <= to;
            if (!include) continue;

            var (band, threshold) = ExpiryBandRules.BandFor(days, thresholds);
            lines.Add((batch, skus.GetValueOrDefault(batch.SkuId), locations.GetValueOrDefault(stock.LocationId), stock.Quantity, days, band, threshold));
        }

        var ordered = lines.OrderBy(l => l.Batch.ExpiryOn).ThenBy(l => l.Sku?.SkuCode ?? l.Batch.SkuCode, StringComparer.Ordinal).ThenBy(l => l.Batch.LotNumber, StringComparer.Ordinal).ToList();

        var rows = ordered.Select(l =>
        {
            ReportMoney? atCost = l.Batch.LandedUnitCost is { } unit ? new ReportMoney(ReportMath.Money(l.Quantity * unit), currencyCode) : null;
            ReportMoney? atRetail = l.Sku is { CurrentPrice: { } price, CurrencyCode: { } priceCurrency } ? new ReportMoney(ReportMath.Money(l.Quantity * price), priceCurrency) : null;

            return new ReportRow
            {
                ["skuCode"] = l.Sku?.SkuCode ?? l.Batch.SkuCode,
                ["productName"] = l.Sku?.ProductName ?? l.Batch.DisplayName,
                ["lotNumber"] = l.Batch.LotNumber,
                ["locationName"] = l.Location?.Name,
                ["expiryOn"] = l.Batch.ExpiryOn,
                ["daysToExpiry"] = (long)l.Days,
                ["band"] = ExpiryBandRules.Label(l.Band, l.Threshold),
                ["quantity"] = l.Quantity,
                ["landedUnitCost"] = l.Batch.LandedUnitCost is { } unitCost ? new ReportMoney(unitCost, currencyCode) : null,
                ["valueAtRiskCost"] = atCost,
                ["valueAtRiskRetail"] = atRetail,
            };
        }).ToList<ReportRow>();

        var totals = new ReportRow { ["skuCode"] = $"{rows.Count} batch lines" };
        var costs = rows.Select(r => r["valueAtRiskCost"]).OfType<ReportMoney>().ToList();
        if (costs.Count > 0) totals["valueAtRiskCost"] = new ReportMoney(costs.Sum(c => c.Amount), currencyCode);
        foreach (var group in rows.Select(r => r["valueAtRiskRetail"]).OfType<ReportMoney>().GroupBy(m => m.CurrencyCode).Take(1))
            totals["valueAtRiskRetail"] = new ReportMoney(group.Sum(m => m.Amount), group.Key);

        var notes = new List<ReportNote>();
        var unknown = ordered.Count(l => l.Batch.LandedUnitCost is null);
        if (unknown > 0)
            notes.Add(new ReportNote("ValueAtRiskCoverage", $"{unknown} of {ordered.Count} batch lines have no landed cost yet, so their value at risk at cost is not shown and is left out of the total."));

        return new StockReportOutput(rows, totals, notes);
    }
}
