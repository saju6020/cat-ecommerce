using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.QueryHandlers.Reporting;

/// <summary>HO-RPT-001 section 15 C -- U-C1 to U-C3 (and U-C4's "no reorder arithmetic of its own"): the stock builders are pure over the view rows.</summary>
public sealed class StockReportBuilderTests
{
    private static readonly Guid Sku = Guid.NewGuid();
    private static readonly Guid CostedBatch = Guid.NewGuid();
    private static readonly Guid UncostedBatch = Guid.NewGuid();
    private static readonly Guid Warehouse = Guid.NewGuid();
    private static readonly Guid Shop = Guid.NewGuid();

    private static IReadOnlyDictionary<Guid, RvBatchRow> Batches(DateOnly? expiry = null) => new Dictionary<Guid, RvBatchRow>
    {
        [CostedBatch] = new() { BatchId = CostedBatch, SkuId = Sku, SkuCode = "SKU-1", DisplayName = "Food", LotNumber = "LOT-A", ExpiryOn = expiry, Status = BatchStatus.Available.Value, LandedUnitCost = 12.5m },
        [UncostedBatch] = new() { BatchId = UncostedBatch, SkuId = Sku, SkuCode = "SKU-1", DisplayName = "Food", LotNumber = "LOT-B", ExpiryOn = expiry, Status = BatchStatus.Available.Value, LandedUnitCost = null },
    };

    private static readonly IReadOnlyDictionary<Guid, RvSkuRow> Skus = new Dictionary<Guid, RvSkuRow>
    {
        [Sku] = new() { SkuId = Sku, SkuCode = "SKU-1", ProductName = "Cat food", Category = ProductCategory.CatFood.Value, CurrentPrice = 20m, CurrencyCode = "BDT" },
    };

    private static readonly IReadOnlyDictionary<Guid, RvStockLocationRow> Locations = new Dictionary<Guid, RvStockLocationRow>
    {
        [Warehouse] = new() { LocationId = Warehouse, Code = "WH", Name = "Warehouse" },
        [Shop] = new() { LocationId = Shop, Code = "SH", Name = "Shop floor" },
    };

    // ── U-C1: opening + in - out +/- adjustments = closing; a transfer is out here and in there ─────────
    [Fact]
    public void U_C1_TheRollForward_Balances_PerBatchAndLocation_AndATransferIsOutAtOneLocationAndInAtTheOther()
    {
        var ledger = new[]
        {
            // Warehouse: 10 received before the range; inside the range 3 sold, 2 transferred out, 1 adjusted down, 5 more received.
            new LedgerAggregate(CostedBatch, Warehouse, MovementKind.Receipt.Value, 10m, 5m),
            new LedgerAggregate(CostedBatch, Warehouse, MovementKind.Sale.Value, 0m, -3m),
            new LedgerAggregate(CostedBatch, Warehouse, MovementKind.TransferOut.Value, 0m, -2m),
            new LedgerAggregate(CostedBatch, Warehouse, MovementKind.AdjustmentOut.Value, 0m, -1m),
            // Shop floor receives the 2.
            new LedgerAggregate(CostedBatch, Shop, MovementKind.TransferIn.Value, 0m, 2m),
        };

        var output = StockPositionBuilder.Build(ledger, Batches(), Skus, Locations, includeZero: false, "BDT");

        var warehouse = output.Rows.Single(r => (string?)r["locationName"] == "Warehouse");
        Assert.Equal(10m, warehouse["openingQuantity"]);
        Assert.Equal(5m, warehouse["receivedQuantity"]);
        Assert.Equal(5m, warehouse["issuedQuantity"]); // 3 sold + 2 transferred out
        Assert.Equal(-1m, warehouse["adjustedQuantity"]);
        Assert.Equal(9m, warehouse["closingQuantity"]);

        var shop = output.Rows.Single(r => (string?)r["locationName"] == "Shop floor");
        Assert.Equal(0m, shop["openingQuantity"]);
        Assert.Equal(2m, shop["receivedQuantity"]); // the other half of the transfer
        Assert.Equal(2m, shop["closingQuantity"]);
    }

    [Fact]
    public void U_C1_ABatchThatSoldOutInTheRange_IsStillListed_AndIncludeZeroControlsTheIdleOnes()
    {
        var soldOut = new[] { new LedgerAggregate(CostedBatch, Warehouse, MovementKind.Receipt.Value, 4m, 0m), new LedgerAggregate(CostedBatch, Warehouse, MovementKind.Sale.Value, 0m, -4m) };
        var idleZero = new[] { new LedgerAggregate(UncostedBatch, Warehouse, MovementKind.Receipt.Value, 3m, 0m), new LedgerAggregate(UncostedBatch, Warehouse, MovementKind.Sale.Value, -3m, 0m) };

        Assert.Single(StockPositionBuilder.Build(soldOut, Batches(), Skus, Locations, false, "BDT").Rows);
        Assert.Empty(StockPositionBuilder.Build(idleZero, Batches(), Skus, Locations, false, "BDT").Rows);
        Assert.Single(StockPositionBuilder.Build(idleZero, Batches(), Skus, Locations, true, "BDT").Rows);
    }

    // ── U-C2: valuation only where a landed cost exists, with the coverage stated ───────────────────────
    [Fact]
    public void U_C2_ABatchWithoutALandedCost_HasANullValuation_TheTotalExcludesIt_AndACoverageNoteSaysSo()
    {
        var ledger = new[]
        {
            new LedgerAggregate(CostedBatch, Warehouse, MovementKind.Receipt.Value, 0m, 10m),
            new LedgerAggregate(UncostedBatch, Warehouse, MovementKind.Receipt.Value, 0m, 6m),
        };

        var output = StockPositionBuilder.Build(ledger, Batches(), Skus, Locations, false, "BDT");

        var costed = output.Rows.Single(r => (string?)r["lotNumber"] == "LOT-A");
        var uncosted = output.Rows.Single(r => (string?)r["lotNumber"] == "LOT-B");
        Assert.Equal(new ReportMoney(125.00m, "BDT"), costed["valuation"]);
        Assert.Null(uncosted["valuation"]); // never zero
        Assert.Null(uncosted["landedUnitCost"]);
        Assert.Equal(new ReportMoney(125.00m, "BDT"), output.Totals["valuation"]);

        var note = Assert.Single(output.Notes);
        Assert.Equal("ValuationCoverage", note.Code);
        Assert.Contains("1 of 2", note.Text);
        Assert.Contains("50.0 %", note.Text);
    }

    [Fact]
    public void U_C2_WithNoCostedStockAtAll_TheTotalHasNoValuation_NotZero()
    {
        var output = StockPositionBuilder.Build([new LedgerAggregate(UncostedBatch, Warehouse, MovementKind.Receipt.Value, 0m, 6m)], Batches(), Skus, Locations, false, "BDT");

        Assert.False(output.Totals.ContainsKey("valuation"));
        Assert.Contains("not available", Assert.Single(output.Notes).Text);
    }

    // ── U-C3: the bands against the configured thresholds ──────────────────────────────────────────────────
    [Theory]
    [InlineData(91, null, null)]
    [InlineData(90, "Within90Days", 90)]
    [InlineData(61, "Within90Days", 90)]
    [InlineData(60, "Within60Days", 60)]
    [InlineData(31, "Within60Days", 60)]
    [InlineData(30, "Within30Days", 30)]
    [InlineData(0, "Within30Days", 30)]
    [InlineData(-1, "Expired", null)]
    public void U_C3_TheBand_IsTheTightestThresholdCrossed(int days, string? band, int? threshold)
    {
        var (actualBand, actualThreshold) = ExpiryBandRules.BandFor(days, [90, 60, 30]);

        Assert.Equal(band, actualBand?.DisplayName);
        Assert.Equal(threshold, actualThreshold);
    }

    [Fact]
    public void U_C3_TheLabel_UsesTheThresholdActuallyConfigured()
    {
        var (band, threshold) = ExpiryBandRules.BandFor(40, [60, 45, 20]);

        Assert.Equal("Within 45 days", ExpiryBandRules.Label(band, threshold));
        Assert.Equal("Expired", ExpiryBandRules.Label(ExpiryBand.Expired, null));
        Assert.Null(ExpiryBandRules.Label(null, null));
    }

    [Fact]
    public void U_C3_TheNearExpiryReport_ListsTheWindowAndTheExpired_WithValueAtCostAndAtRetail()
    {
        var today = new DateOnly(2026, 10, 14);
        var batches = new Dictionary<Guid, RvBatchRow>
        {
            [CostedBatch] = new() { BatchId = CostedBatch, SkuId = Sku, SkuCode = "SKU-1", LotNumber = "SOON", ExpiryOn = today.AddDays(20), LandedUnitCost = 10m },
            [UncostedBatch] = new() { BatchId = UncostedBatch, SkuId = Sku, SkuCode = "SKU-1", LotNumber = "GONE", ExpiryOn = today.AddDays(-5), LandedUnitCost = null },
        };
        var onHand = new[] { new OnHandLine(CostedBatch, Warehouse, 4m), new OnHandLine(UncostedBatch, Warehouse, 2m), new OnHandLine(CostedBatch, Shop, 0m) };

        var withExpired = NearExpiryBuilder.Build(onHand, batches, Skus, Locations, today, today.AddDays(90), today, includeExpired: true, [90, 60, 30], "BDT");
        var withoutExpired = NearExpiryBuilder.Build(onHand, batches, Skus, Locations, today, today.AddDays(90), today, includeExpired: false, [90, 60, 30], "BDT");

        Assert.Equal(["GONE", "SOON"], withExpired.Rows.Select(r => (string)r["lotNumber"]!));
        Assert.Equal(["SOON"], withoutExpired.Rows.Select(r => (string)r["lotNumber"]!));

        var soon = withExpired.Rows.Single(r => (string)r["lotNumber"]! == "SOON");
        Assert.Equal("Within 30 days", soon["band"]);
        Assert.Equal(20L, soon["daysToExpiry"]);
        Assert.Equal(new ReportMoney(40.00m, "BDT"), soon["valueAtRiskCost"]);
        Assert.Equal(new ReportMoney(80.00m, "BDT"), soon["valueAtRiskRetail"]);

        var gone = withExpired.Rows.Single(r => (string)r["lotNumber"]! == "GONE");
        Assert.Equal("Expired", gone["band"]);
        Assert.Null(gone["valueAtRiskCost"]);
        Assert.Equal(new ReportMoney(40.00m, "BDT"), gone["valueAtRiskRetail"]);
        Assert.Equal(new ReportMoney(40.00m, "BDT"), withExpired.Totals["valueAtRiskCost"]); // the total leaves the uncosted one out
        Assert.Single(withExpired.Notes);
    }

    // ── U-C4: the suggestion is HO-IMP-001's, not re-implemented here ─────────────────────────────────────────
    [Fact]
    public void U_C4_TheLowStockSource_CallsTheInventoryCalculator_AndHasNoReorderArithmeticOfItsOwn()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Reporting", "Sources", "StockReportSources.cs"));
        var lowStock = source[source.IndexOf("class StockLowReorderSource", StringComparison.Ordinal)..source.IndexOf("class ImportConsignmentsSource", StringComparison.Ordinal)];

        Assert.Contains("calculator.Suggest(", lowStock);
        Assert.Contains("StockUsability.Usable(", lowStock);
        Assert.DoesNotContain("ReorderReads", lowStock);
        Assert.DoesNotContain("db.Suppliers", lowStock);
        Assert.DoesNotContain("db.StockPositions", lowStock);
        Assert.DoesNotContain("Math.Ceiling", lowStock);
        Assert.DoesNotContain("Math.Max(0m", lowStock);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
