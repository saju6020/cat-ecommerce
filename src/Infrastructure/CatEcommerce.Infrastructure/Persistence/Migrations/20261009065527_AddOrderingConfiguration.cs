using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderingConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ordering");

            migrationBuilder.CreateTable(
                name: "bank_transfer_account",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    branch_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    routing_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_bank_transfer_account", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "courier",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tracking_url_template = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supports_cod = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_courier", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_zone",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    cod_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    cod_max_order_value = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
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
                    table.PrimaryKey("PK_delivery_zone", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "discount_code",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    percent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    minimum_spend = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    product_ids = table.Column<string>(type: "jsonb", nullable: false),
                    category_ids = table.Column<string>(type: "jsonb", nullable: false),
                    first_order_only = table.Column<bool>(type: "boolean", nullable: false),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    per_customer_limit = table.Column<int>(type: "integer", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    times_redeemed = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_discount_code", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_zone_coverage",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_codes = table.Column<string>(type: "jsonb", nullable: false),
                    delivery_zone_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_zone_coverage", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_zone_coverage_delivery_zone_delivery_zone_id",
                        column: x => x.delivery_zone_id,
                        principalSchema: "ordering",
                        principalTable: "delivery_zone",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_zone_service",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_level = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    base_charge = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    free_over_order_value = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    lead_time_days = table.Column<int>(type: "integer", nullable: false),
                    weight_bands = table.Column<string>(type: "jsonb", nullable: false),
                    slot_windows = table.Column<string>(type: "jsonb", nullable: false),
                    delivery_zone_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_zone_service", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_zone_service_delivery_zone_delivery_zone_id",
                        column: x => x.delivery_zone_id,
                        principalSchema: "ordering",
                        principalTable: "delivery_zone",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_courier_name",
                schema: "ordering",
                table: "courier",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_zone_branch_active",
                schema: "ordering",
                table: "delivery_zone",
                columns: new[] { "branch_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_zone_name",
                schema: "ordering",
                table: "delivery_zone",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_zone_coverage_zone",
                schema: "ordering",
                table: "delivery_zone_coverage",
                column: "delivery_zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_zone_service_level",
                schema: "ordering",
                table: "delivery_zone_service",
                columns: new[] { "delivery_zone_id", "service_level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_discount_code_code",
                schema: "ordering",
                table: "discount_code",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transfer_account",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "courier",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "delivery_zone_coverage",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "delivery_zone_service",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "discount_code",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "delivery_zone",
                schema: "ordering");
        }
    }
}
