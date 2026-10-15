using System;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLocationsAndStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "product_storage_profile",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    condition = table.Column<int>(type: "integer", nullable: false),
                    min_temperature_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    max_temperature_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    handling_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_product_storage_profile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_location",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_stock_location", x => x.Id);
                });

            // HO-IMP-001 §8.3 -- the five locations of the seeded MAIN branch, with the fixed ids of InventorySeed. Every kind is written
            // from the enumeration in C#, never typed as a literal (U-X2).
            var seededOn = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
            var seed = new (Guid Id, string Code, string Name, StockLocationKind Kind)[]
            {
                (InventorySeed.ShopLocationId, InventorySeed.ShopCode, "Shop floor", StockLocationKind.ShopFloor),
                (InventorySeed.WarehouseLocationId, InventorySeed.WarehouseCode, "Warehouse", StockLocationKind.Warehouse),
                (InventorySeed.DispensaryLocationId, InventorySeed.DispensaryCode, "Clinic dispensary", StockLocationKind.ClinicDispensary),
                (InventorySeed.BoardingStoreLocationId, InventorySeed.BoardingStoreCode, "Boarding store", StockLocationKind.BoardingStore),
                (InventorySeed.QuarantineLocationId, InventorySeed.QuarantineCode, "Quarantine", StockLocationKind.Quarantine),
            };

            foreach (var location in seed)
            {
                migrationBuilder.InsertData(
                    schema: "inventory",
                    table: "stock_location",
                    columns: new[]
                    {
                        "Id", "branch_id", "code", "name", "kind", "is_active",
                        "CreatedBy", "CreatedDate", "Language", "LastUpdatedDate", "LastUpdatedBy", "TenantId", "Tags",
                        "VerticalId", "ServiceId", "IsMarkedToDelete", "Version"
                    },
                    values: new object[]
                    {
                        location.Id, AdministrationSeed.MainBranchId, location.Code, location.Name, location.Kind.Value, true,
                        Guid.Empty, seededOn, null, seededOn, Guid.Empty, Guid.Empty, null,
                        Guid.Empty, null, false, 1
                    });
            }

            migrationBuilder.CreateIndex(
                name: "ix_stock_location_branch_code",
                schema: "inventory",
                table: "stock_location",
                columns: new[] { "branch_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_location_one_quarantine",
                schema: "inventory",
                table: "stock_location",
                column: "branch_id",
                unique: true,
                filter: "kind = 5 AND is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_storage_profile",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_location",
                schema: "inventory");
        }
    }
}
