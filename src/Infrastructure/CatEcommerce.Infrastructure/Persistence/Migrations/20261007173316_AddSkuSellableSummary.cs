using CatEcommerce.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-SHP-001-FU2 / ADR-116 amendment (owner, 2026-10-07): Inventory's trigger-kept per-SKU sellable summary
    /// (<c>inventory.sku_sellable_summary</c>), its read contract <c>inventory.v_sku_sellable</c>, the refresh function and the three
    /// maintenance triggers (stock_position, batch, stock_location), a backfill from current positions, and the rename of the Shop catalogue
    /// view column <c>in_stock</c> to <c>is_on_sale</c> (it never meant stock). The view definition is not rewritten: <c>ALTER VIEW ... RENAME
    /// COLUMN</c> is metadata only. Hand-written SQL lives in <see cref="InventoryLedgerSql"/>.
    /// </summary>
    public partial class AddSkuSellableSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InventoryLedgerSql.CreateSkuSellableSummary());
            migrationBuilder.Sql(InventoryLedgerSql.RefreshSkuSellableFunction());
            migrationBuilder.Sql(InventoryLedgerSql.SummaryGuardFunctionAndTriggers());
            migrationBuilder.Sql(InventoryLedgerSql.SummaryDeferredRefresh());
            migrationBuilder.Sql(InventoryLedgerSql.SummaryMaintenanceTriggers());
            migrationBuilder.Sql(InventoryLedgerSql.BackfillSkuSellableSummary()); // after the triggers, so no movement can fall between the two
            migrationBuilder.Sql(InventoryLedgerSql.CreateSkuSellableView());
            migrationBuilder.Sql("ALTER VIEW product.storefront_catalogue_entry RENAME COLUMN in_stock TO is_on_sale;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER VIEW product.storefront_catalogue_entry RENAME COLUMN is_on_sale TO in_stock;");
            migrationBuilder.Sql(InventoryLedgerSql.DropSkuSellableSummaryObjects());
        }
    }
}
