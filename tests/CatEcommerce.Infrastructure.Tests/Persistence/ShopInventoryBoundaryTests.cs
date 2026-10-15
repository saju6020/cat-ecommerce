using System.Text.RegularExpressions;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Persistence;

/// <summary>
/// HO-SHP-001 section 8.7 / section 12 (HO-SHP-001-FU2): Shop reads Inventory through its published view
/// <c>inventory.v_sku_sellable</c> (<c>db.SkuSellable</c>) and NOTHING else of Inventory's -- no table, no ledger row, no batch, no
/// Inventory aggregate -- and never writes it. A source scan of every Shop read-side and availability file.
/// </summary>
public sealed class ShopInventoryBoundaryTests
{
    private static readonly string[] InventoryDbSets =
    [
        "StockLocations", "ProductStorageProfiles", "Suppliers", "PurchaseOrders", "Consignments", "GoodsReceipts", "Batches", "StockLedgerEntries",
        "StockReservationEntries", "StockTransfers", "StockAdjustments", "StockCounts", "InventoryAlerts", "ReorderPolicies", "StorageBreachReports",
        "StockPositions", "BatchUnitCosts"
    ];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("CatEcommerce.sln was not found above the test binaries.");
    }

    private static IEnumerable<string> ShopFiles()
    {
        var root = RepositoryRoot();
        foreach (var folder in new[]
        {
            "src/Read/CatEcommerce.Read/QueryHandlers/Product",
            "src/Infrastructure/CatEcommerce.Infrastructure/Product",
            "src/Application/CatEcommerce.Application/CommandHandlers/Product"
        })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)) yield return file;
        }
    }

    [Fact]
    public void ShopCode_TouchesNoInventoryDbSet_ButTheSellableStockView()
    {
        var offenders = new List<string>();
        foreach (var file in ShopFiles())
        {
            var code = string.Join('\n', File.ReadAllLines(file).Where(line => !line.TrimStart().StartsWith("//") && !line.TrimStart().StartsWith("///")));
            foreach (var dbSet in InventoryDbSets)
                if (Regex.IsMatch(code, $@"\bdb\.{dbSet}\b|\.Set<\w*(Batch|StockLocation|StockLedger|StockPosition|Supplier|PurchaseOrder)\w*>"))
                    offenders.Add($"{Path.GetFileName(file)} reads {dbSet}");
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void ShopCode_UsesNoInventoryAggregateOrLedgerPort()
    {
        var offenders = ShopFiles()
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"Aggregates\.Inventory|IStockLedger|IStockPositionReader|IBatchRegistry"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ShopCode_NeverWritesTheView()
    {
        var offenders = ShopFiles()
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"SellableStock\s*\.\s*(Add|Remove|Update|Attach)|ExecuteSql\w*\([^)]*inventory\."))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }
}
