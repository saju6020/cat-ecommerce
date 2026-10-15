using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderingCart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cart",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_kind = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_token_hash = table.Column<string>(type: "char(64)", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    merged_into_cart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_activity_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_cart", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cart_line",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    price_when_added = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    added_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_cart_line_cart_cart_id",
                        column: x => x.cart_id,
                        principalSchema: "ordering",
                        principalTable: "cart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cart_active_customer",
                schema: "ordering",
                table: "cart",
                column: "customer_id",
                unique: true,
                filter: "status = 1 AND customer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cart_guest_purge",
                schema: "ordering",
                table: "cart",
                columns: new[] { "owner_kind", "status", "last_activity_on" });

            migrationBuilder.CreateIndex(
                name: "ix_cart_guest_token",
                schema: "ordering",
                table: "cart",
                column: "guest_token_hash",
                unique: true,
                filter: "guest_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cart_line_cart",
                schema: "ordering",
                table: "cart_line",
                column: "cart_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_line",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "cart",
                schema: "ordering");
        }
    }
}
