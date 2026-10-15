using System;
using CatEcommerce.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLandedCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consignment_landed_cost_line",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    purchase_cost_base = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    allocated_cost_base = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    landed_unit_cost_base = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_landed_cost_line", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_landed_cost_line_consignment_consignment_id",
                        column: x => x.consignment_id,
                        principalSchema: "inventory",
                        principalTable: "consignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consignment_landed_cost_line_batch",
                schema: "inventory",
                table: "consignment_landed_cost_line",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_landed_cost_line_consignment_id",
                schema: "inventory",
                table: "consignment_landed_cost_line",
                column: "consignment_id");

            // US-IMP-05 AC2 -- landed ?? purchase, per batch.
            migrationBuilder.Sql(InventoryLedgerSql.CreateBatchUnitCostView());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InventoryLedgerSql.DropBatchUnitCostView());

            migrationBuilder.DropTable(
                name: "consignment_landed_cost_line",
                schema: "inventory");
        }
    }
}
