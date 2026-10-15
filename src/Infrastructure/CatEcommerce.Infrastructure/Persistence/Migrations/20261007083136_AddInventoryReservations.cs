using System;
using CatEcommerce.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_reservation_entry",
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
                    reference_kind = table.Column<int>(type: "integer", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fefo_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_stock_reservation_entry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_reservation_entry_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "inventory",
                        principalTable: "batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_entry_stock_location_location_id",
                        column: x => x.location_id,
                        principalSchema: "inventory",
                        principalTable: "stock_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservation_entry_batch",
                schema: "inventory",
                table: "stock_reservation_entry",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_entry_location_id",
                schema: "inventory",
                table: "stock_reservation_entry",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservation_entry_reference",
                schema: "inventory",
                table: "stock_reservation_entry",
                columns: new[] { "reference_kind", "reference_id", "reference_line_id" });

            // HO-IMP-001 §8.7 / §12.1 / ADR-098..101 -- the reservation entry's sign check, the reservation apply trigger, the usability trigger
            // (the SAME function as the ledger's), the immutability triggers and the published sellable view (ADR-116).
            migrationBuilder.Sql(InventoryLedgerSql.AddReservationSignCheck());
            migrationBuilder.Sql(InventoryLedgerSql.ApplyReservationFunctionAndTrigger());
            migrationBuilder.Sql(InventoryLedgerSql.BatchUsableReservationTrigger());
            migrationBuilder.Sql(InventoryLedgerSql.RefuseMutationReservationTriggers());
            migrationBuilder.Sql(InventoryLedgerSql.CreateSellableStockView());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InventoryLedgerSql.DropReservationObjects());

            migrationBuilder.DropTable(
                name: "stock_reservation_entry",
                schema: "inventory");
        }
    }
}
