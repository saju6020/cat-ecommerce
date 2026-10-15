using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingAccountingExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_export_run",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    range_from = table.Column<DateOnly>(type: "date", nullable: false),
                    range_to = table.Column<DateOnly>(type: "date", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_count = table.Column<int>(type: "integer", nullable: false),
                    invoice_line_count = table.Column<int>(type: "integer", nullable: false),
                    payment_count = table.Column<int>(type: "integer", nullable: false),
                    allocation_count = table.Column<int>(type: "integer", nullable: false),
                    credit_note_count = table.Column<int>(type: "integer", nullable: false),
                    refund_count = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    run_by = table.Column<Guid>(type: "uuid", nullable: false),
                    run_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_accounting_export_run", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_export_run_run_on",
                schema: "billing",
                table: "accounting_export_run",
                column: "run_on");

            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.AppendOnly("accounting_export_run"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropAppendOnly("accounting_export_run"));

            migrationBuilder.DropTable(
                name: "accounting_export_run",
                schema: "billing");
        }
    }
}
