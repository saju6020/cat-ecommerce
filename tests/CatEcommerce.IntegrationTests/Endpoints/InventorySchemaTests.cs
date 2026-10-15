using CatEcommerce.Shared.Common.Constants;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 §16 I-A1 -- the migrations on an empty database produce schema <c>inventory</c> with every table, index and view of §12.1,
/// the triggers, <c>ck_stock_position_balance</c> and the five seeded locations for <c>MAIN</c>. The expected lists grow with each phase
/// (the migration of that phase adds its names here); by Phase J they are all of §12.1.
/// </summary>
public sealed class InventorySchemaTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private static readonly string[] Tables =
    [
        "stock_location", "product_storage_profile",
        "supplier", "purchase_order", "purchase_order_line",
        "consignment", "consignment_hs_code", "consignment_cost", "consignment_exchange_rate", "consignment_document", "consignment_status_change",
        "goods_receipt", "goods_receipt_line", "batch", "batch_status_change", "stock_ledger_entry", "stock_position", "stock_reservation_entry",
        "stock_transfer", "stock_transfer_line", "stock_adjustment", "stock_adjustment_line", "stock_count", "stock_count_line",
        "consignment_landed_cost_line",
        "stock_alert", "reorder_policy", "storage_breach_report",
    ];

    private static readonly string[] Indexes =
    [
        "ix_stock_location_branch_code", "ix_stock_location_one_quarantine",
        "ix_supplier_code", "ix_purchase_order_number", "ix_purchase_order_branch_status", "ix_purchase_order_supplier", "ix_purchase_order_line_order_sku",
        "ix_consignment_reference", "ix_consignment_branch_status", "ix_consignment_exchange_rate_currency",
        "ix_goods_receipt_number", "ix_goods_receipt_line_purchase_order_line", "ix_batch_receipt_lot", "ix_batch_sku_status_expires_on", "ix_batch_consignment", "ix_batch_lot_number",
        "ix_stock_ledger_entry_batch_sequence", "ix_stock_ledger_entry_sku_location_occurred_on", "ix_stock_ledger_entry_reference", "ix_stock_ledger_entry_occurred_on",
        "ix_stock_ledger_entry_customer", "ix_stock_ledger_entry_cat", "ix_stock_position_sku", "ix_stock_reservation_entry_reference", "ix_stock_reservation_entry_batch",
        "ix_stock_transfer_number", "ix_stock_transfer_branch_transferred_on", "ix_stock_adjustment_number", "ix_stock_adjustment_branch_status",
        "ix_stock_count_number", "ix_stock_count_one_active", "ix_stock_count_line_count_sku_batch",
        "ix_consignment_landed_cost_line_batch",
        "ix_stock_alert_dedup_key", "ix_stock_alert_one_live_per_pair", "ix_stock_alert_branch_status_kind", "ix_reorder_policy_sku_location", "ix_batch_recall_number",
        "ix_storage_breach_report_branch_status", "ix_storage_breach_report_location",
    ];

    private static readonly string[] Views = ["v_sellable_stock", "v_batch_unit_cost"];

    private static readonly string[] Triggers =
    [
        "tg_stock_ledger_apply", "tg_stock_position_guard", "tg_stock_batch_usable", "tg_stock_ledger_immutable", "tg_stock_ledger_no_truncate",
        "tg_stock_reservation_apply", "tg_stock_reservation_immutable", "tg_stock_reservation_no_truncate",
    ];

    private static readonly string[] Constraints = ["ck_stock_position_balance", "ck_stock_ledger_sign", "ck_stock_reservation_sign"];

    private static readonly string[] Sequences = ["po_number_seq", "consignment_number_seq", "grn_number_seq", "transfer_number_seq", "adjustment_number_seq", "count_number_seq", "recall_number_seq"];

    private static async Task<List<string>> ColumnAsync(string sql)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    [Fact]
    public async Task I_A1_TheInventorySchemaHasEveryTableIndexViewTriggerConstraintAndSequenceOfThePhasesBuiltSoFar()
    {
        var tables = await ColumnAsync("SELECT table_name FROM information_schema.tables WHERE table_schema = 'inventory' AND table_type = 'BASE TABLE'");
        var indexes = await ColumnAsync("SELECT indexname FROM pg_indexes WHERE schemaname = 'inventory'");
        var views = await ColumnAsync("SELECT table_name FROM information_schema.views WHERE table_schema = 'inventory'");
        var triggers = await ColumnAsync(
            "SELECT t.tgname FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'inventory' AND NOT t.tgisinternal");
        var constraints = await ColumnAsync(
            "SELECT con.conname FROM pg_constraint con JOIN pg_namespace n ON n.oid = con.connamespace WHERE n.nspname = 'inventory'");
        var sequences = await ColumnAsync("SELECT sequencename FROM pg_sequences WHERE schemaname = 'inventory'");

        Assert.All(Tables, t => Assert.Contains(t, tables));
        Assert.All(Indexes, i => Assert.Contains(i, indexes));
        Assert.All(Views, v => Assert.Contains(v, views));
        Assert.All(Triggers, t => Assert.Contains(t, triggers));
        Assert.All(Constraints, c => Assert.Contains(c, constraints));
        Assert.All(Sequences, s => Assert.Contains(s, sequences));
    }

    [Fact]
    public async Task I_A1_TheMainBranchHasTheFiveSeededLocations()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"Id\", code, kind, is_active FROM inventory.stock_location WHERE branch_id = @branch ORDER BY code", connection);
        command.Parameters.AddWithValue("branch", AdministrationSeed.MainBranchId);

        var rows = new List<(Guid Id, string Code, int Kind, bool Active)>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3)));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.True(r.Active));
        Assert.Contains(rows, r => r.Id == InventorySeed.QuarantineLocationId && r.Code == InventorySeed.QuarantineCode && r.Kind == CatEcommerce.Shared.Common.Enums.StockLocationKind.Quarantine.Value);
        Assert.Contains(rows, r => r.Id == InventorySeed.ShopLocationId && r.Code == InventorySeed.ShopCode);
        Assert.Contains(rows, r => r.Id == InventorySeed.WarehouseLocationId && r.Code == InventorySeed.WarehouseCode);
        Assert.Contains(rows, r => r.Id == InventorySeed.DispensaryLocationId && r.Code == InventorySeed.DispensaryCode);
        Assert.Contains(rows, r => r.Id == InventorySeed.BoardingStoreLocationId && r.Code == InventorySeed.BoardingStoreCode);
    }
}
