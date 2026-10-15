using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Inventory (HO-IMP-001) DbSets -- one per root of §8.1. See ADR-341 for the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<StockLocationAggregate> StockLocations => Set<StockLocationAggregate>();
    public DbSet<ProductStorageProfileAggregate> ProductStorageProfiles => Set<ProductStorageProfileAggregate>();
    public DbSet<SupplierAggregate> Suppliers => Set<SupplierAggregate>();
    public DbSet<PurchaseOrderAggregate> PurchaseOrders => Set<PurchaseOrderAggregate>();
    public DbSet<ConsignmentAggregate> Consignments => Set<ConsignmentAggregate>();
    public DbSet<GoodsReceiptAggregate> GoodsReceipts => Set<GoodsReceiptAggregate>();
    public DbSet<BatchAggregate> Batches => Set<BatchAggregate>();
    public DbSet<StockLedgerEntryAggregate> StockLedgerEntries => Set<StockLedgerEntryAggregate>();
    public DbSet<StockReservationEntryAggregate> StockReservationEntries => Set<StockReservationEntryAggregate>();
    public DbSet<StockTransferAggregate> StockTransfers => Set<StockTransferAggregate>();
    public DbSet<StockAdjustmentAggregate> StockAdjustments => Set<StockAdjustmentAggregate>();
    public DbSet<StockCountAggregate> StockCounts => Set<StockCountAggregate>();

    /// <summary>Inventory's alerts (expiry, low stock, shortfall, breach, mismatch). Named apart from <c>StockAlerts</c>, which is Shop's back-in-stock alert.</summary>
    public DbSet<InventoryAlertAggregate> InventoryAlerts => Set<InventoryAlertAggregate>();
    public DbSet<ReorderPolicyAggregate> ReorderPolicies => Set<ReorderPolicyAggregate>();
    public DbSet<StorageBreachReportAggregate> StorageBreachReports => Set<StorageBreachReportAggregate>();

    /// <summary>The ledger's projection: keyless and excluded from migrations -- readable, never writable through EF (ADR-099).</summary>
    public DbSet<StockPositionEntry> StockPositions => Set<StockPositionEntry>();

    /// <summary><c>inventory.v_sellable_stock</c> (ADR-116): per SKU per branch, the sellable quantity and the FEFO-first expiry. A keyless view.</summary>
    public DbSet<SellableStockEntry> SellableStock => Set<SellableStockEntry>();

    /// <summary><c>inventory.v_sku_sellable</c> (ADR-116 amendment 2026-10-07): the trigger-kept summary per SKU, branch and expiry date, expired stock already excluded. A keyless view.</summary>
    public DbSet<SkuSellableEntry> SkuSellable => Set<SkuSellableEntry>();

    /// <summary><c>inventory.v_batch_unit_cost</c> (§8.11): per batch the unit cost in base currency and its basis (<c>Landed</c> or <c>Purchase</c>). A keyless view.</summary>
    public DbSet<BatchUnitCostEntry> BatchUnitCosts => Set<BatchUnitCostEntry>();
}
