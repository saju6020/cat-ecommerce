using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStockControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_adjustment",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    value_base = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_stock_adjustment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_count",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    sku_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    variance_value_base = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    opened_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    opened_by = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    commit_requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    committed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_stock_count", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    transferred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    transferred_by = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_stock_transfer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustment_line",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    unit_cost_base = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    stock_adjustment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustment_line", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_adjustment_line_stock_adjustment_stock_adjustment_id",
                        column: x => x.stock_adjustment_id,
                        principalSchema: "inventory",
                        principalTable: "stock_adjustment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_count_line",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counted_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    book_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    variance = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    unit_cost_base = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    counted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    counted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    stock_count_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_count_line", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_count_line_stock_count_stock_count_id",
                        column: x => x.stock_count_id,
                        principalSchema: "inventory",
                        principalTable: "stock_count",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_line",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    stock_transfer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_line", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_line_stock_transfer_stock_transfer_id",
                        column: x => x.stock_transfer_id,
                        principalSchema: "inventory",
                        principalTable: "stock_transfer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_branch_status",
                schema: "inventory",
                table: "stock_adjustment",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_number",
                schema: "inventory",
                table: "stock_adjustment",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_line_batch",
                schema: "inventory",
                table: "stock_adjustment_line",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_adjustment_line_stock_adjustment_id",
                schema: "inventory",
                table: "stock_adjustment_line",
                column: "stock_adjustment_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_count_branch_status",
                schema: "inventory",
                table: "stock_count",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_count_number",
                schema: "inventory",
                table: "stock_count",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_count_one_active",
                schema: "inventory",
                table: "stock_count",
                column: "location_id",
                unique: true,
                filter: "status IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "ix_stock_count_line_count_sku_batch",
                schema: "inventory",
                table: "stock_count_line",
                columns: new[] { "stock_count_id", "sku_id", "batch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_branch_transferred_on",
                schema: "inventory",
                table: "stock_transfer",
                columns: new[] { "branch_id", "transferred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_number",
                schema: "inventory",
                table: "stock_transfer",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_line_batch",
                schema: "inventory",
                table: "stock_transfer_line",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_line_stock_transfer_id",
                schema: "inventory",
                table: "stock_transfer_line",
                column: "stock_transfer_id");

            // ADR-114 -- TRF-/ADJ-/CNT-{yyyy}-{seq:D6}; not gapless.
            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS inventory.transfer_number_seq START WITH 1 INCREMENT BY 1;");
            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS inventory.adjustment_number_seq START WITH 1 INCREMENT BY 1;");
            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS inventory.count_number_seq START WITH 1 INCREMENT BY 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_adjustment_line",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_count_line",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_transfer_line",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_adjustment",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_count",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_transfer",
                schema: "inventory");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS inventory.transfer_number_seq;");
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS inventory.adjustment_number_seq;");
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS inventory.count_number_seq;");
        }
    }
}
