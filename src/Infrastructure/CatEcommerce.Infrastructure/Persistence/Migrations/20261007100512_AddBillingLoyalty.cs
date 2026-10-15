using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loyalty_account",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_loyalty_account", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_entry",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shortfall = table.Column<int>(type: "integer", nullable: false),
                    idempotent_source = table.Column<bool>(type: "boolean", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_loyalty_entry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_entry_customer_occurred_on",
                schema: "billing",
                table: "loyalty_entry",
                columns: new[] { "customer_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_entry_kind_credit_note",
                schema: "billing",
                table: "loyalty_entry",
                columns: new[] { "kind", "credit_note_id" },
                unique: true,
                filter: "idempotent_source AND credit_note_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_entry_kind_invoice",
                schema: "billing",
                table: "loyalty_entry",
                columns: new[] { "kind", "invoice_id" },
                unique: true,
                filter: "idempotent_source AND invoice_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_entry_kind_source_customer",
                schema: "billing",
                table: "loyalty_entry",
                columns: new[] { "kind", "source_customer_id" },
                unique: true,
                filter: "idempotent_source AND source_customer_id IS NOT NULL");

            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.AppendOnly("loyalty_entry"));
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.NoDelete("loyalty_account"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropNoDelete("loyalty_account"));
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropAppendOnly("loyalty_entry"));

            migrationBuilder.DropTable(
                name: "loyalty_account",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "loyalty_entry",
                schema: "billing");
        }
    }
}
