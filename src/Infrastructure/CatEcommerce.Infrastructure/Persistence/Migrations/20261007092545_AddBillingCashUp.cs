using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingCashUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_up",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    counted_cash = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    counted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    counted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    variance = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    expected_cash_total = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    expected_cash_count = table.Column<int>(type: "integer", nullable: true),
                    expected_card_total = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    expected_card_count = table.Column<int>(type: "integer", nullable: true),
                    expected_transfer_total = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    expected_transfer_count = table.Column<int>(type: "integer", nullable: true),
                    expected_cash_refunds = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    expected_cash_reversals = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    expected_carried_in = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    expected_cash = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
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
                    table.PrimaryKey("PK_cash_up", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_up_branch_date",
                schema: "billing",
                table: "cash_up",
                columns: new[] { "branch_id", "business_date" },
                unique: true);

            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.ClosedCashUpImmutability());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropClosedCashUpImmutability());

            migrationBuilder.DropTable(
                name: "cash_up",
                schema: "billing");
        }
    }
}
