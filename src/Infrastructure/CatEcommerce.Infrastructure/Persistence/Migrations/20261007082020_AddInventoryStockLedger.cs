using System;
using CatEcommerce.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "goods_receipt",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    received_by = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: true),
                    VerticalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: true),
                    IsMarkedToDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "batch",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    stock_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stock_class = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    manufactured_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    received_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    purchase_unit_cost_amount = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    purchase_unit_cost_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    purchase_unit_cost_base = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    status_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status_changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    status_changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: true),
                    VerticalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: true),
                    IsMarkedToDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_batch_goods_receipt_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inventory",
                        principalTable: "goods_receipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_line",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    stock_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    manufactured_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity_accepted = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    quantity_rejected = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    rejection_reason = table.Column<int>(type: "integer", nullable: true),
                    rejection_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_for_inspection = table.Column<bool>(type: "boolean", nullable: false),
                    unit_cost_amount = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    unit_cost_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    exchange_rate_to_base = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_line", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goods_receipt_line_goods_receipt_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inventory",
                        principalTable: "goods_receipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "batch_status_change",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_status_change", x => x.Id);
                    table.ForeignKey(
                        name: "FK_batch_status_change_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "inventory",
                        principalTable: "batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_ledger_entry",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    consumes_reservation = table.Column<bool>(type: "boolean", nullable: false),
                    reference_kind = table.Column<int>(type: "integer", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fefo_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: true),
                    VerticalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: true),
                    IsMarkedToDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_ledger_entry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_ledger_entry_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "inventory",
                        principalTable: "batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_ledger_entry_stock_location_location_id",
                        column: x => x.location_id,
                        principalSchema: "inventory",
                        principalTable: "stock_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_batch_branch_status",
                schema: "inventory",
                table: "batch",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_batch_consignment",
                schema: "inventory",
                table: "batch",
                column: "consignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_lot_number",
                schema: "inventory",
                table: "batch",
                column: "lot_number");

            migrationBuilder.CreateIndex(
                name: "ix_batch_receipt_lot",
                schema: "inventory",
                table: "batch",
                columns: new[] { "goods_receipt_id", "sku_id", "lot_number", "expires_on" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_batch_sku_status_expires_on",
                schema: "inventory",
                table: "batch",
                columns: new[] { "sku_id", "status", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "IX_batch_status_change_batch_id",
                schema: "inventory",
                table: "batch_status_change",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_branch_received_on",
                schema: "inventory",
                table: "goods_receipt",
                columns: new[] { "branch_id", "received_on" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_consignment",
                schema: "inventory",
                table: "goods_receipt",
                column: "consignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_number",
                schema: "inventory",
                table: "goods_receipt",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_purchase_order",
                schema: "inventory",
                table: "goods_receipt",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_line_batch",
                schema: "inventory",
                table: "goods_receipt_line",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_line_goods_receipt_id",
                schema: "inventory",
                table: "goods_receipt_line",
                column: "goods_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_line_purchase_order_line",
                schema: "inventory",
                table: "goods_receipt_line",
                column: "purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_ledger_entry_batch_sequence",
                schema: "inventory",
                table: "stock_ledger_entry",
                columns: new[] { "batch_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_ledger_entry_branch_kind_occurred_on",
                schema: "inventory",
                table: "stock_ledger_entry",
                columns: new[] { "branch_id", "kind", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_ledger_entry_cat",
                schema: "inventory",
                table: "stock_ledger_entry",
                column: "cat_id",
                filter: "cat_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_ledger_entry_customer",
                schema: "inventory",
                table: "stock_ledger_entry",
                column: "customer_id",
                filter: "customer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entry_location_id",
                schema: "inventory",
                table: "stock_ledger_entry",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_ledger_entry_occurred_on",
                schema: "inventory",
                table: "stock_ledger_entry",
                column: "occurred_on");

            migrationBuilder.CreateIndex(
                name: "ix_stock_ledger_entry_reference",
                schema: "inventory",
                table: "stock_ledger_entry",
                columns: new[] { "reference_kind", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_ledger_entry_sku_location_occurred_on",
                schema: "inventory",
                table: "stock_ledger_entry",
                columns: new[] { "sku_id", "location_id", "occurred_on" });

            // HO-IMP-001 §8.7 / §12.1 / ADR-098..101 -- the projection, its CHECK, the apply and guard triggers, the usability trigger and the
            // immutability triggers. The SQL lives in InventoryLedgerSql so every enumeration value is written from the enumeration (U-X2).
            migrationBuilder.Sql(InventoryLedgerSql.CreateStockPosition());
            migrationBuilder.Sql(InventoryLedgerSql.AddLedgerSignCheck());
            migrationBuilder.Sql(InventoryLedgerSql.PositionGuardFunctionAndTrigger());
            migrationBuilder.Sql(InventoryLedgerSql.ApplyLedgerFunctionAndTrigger());
            migrationBuilder.Sql(InventoryLedgerSql.BatchUsableFunctionAndLedgerTrigger());
            migrationBuilder.Sql(InventoryLedgerSql.RefuseMutationFunctionAndLedgerTriggers());

            // ADR-114 -- GRN-{yyyy}-{seq:D6}; not gapless.
            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS inventory.grn_number_seq START WITH 1 INCREMENT BY 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Triggers, functions and the projection go first: the projection references batch and stock_location.
            migrationBuilder.Sql(InventoryLedgerSql.DropStockLedgerObjects());

            migrationBuilder.DropTable(
                name: "batch_status_change",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "goods_receipt_line",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_ledger_entry",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "batch",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "goods_receipt",
                schema: "inventory");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS inventory.grn_number_seq;");
        }
    }
}
