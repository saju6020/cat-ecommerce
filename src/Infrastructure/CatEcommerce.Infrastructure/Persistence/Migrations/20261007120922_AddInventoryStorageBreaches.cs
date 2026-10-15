using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStorageBreaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_breach_report",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    condition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    temperature_c = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    affected_batch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    quarantined_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    quarantined_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_storage_breach_report", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_storage_breach_report_branch_status",
                schema: "inventory",
                table: "storage_breach_report",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_storage_breach_report_location",
                schema: "inventory",
                table: "storage_breach_report",
                column: "location_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_breach_report",
                schema: "inventory");
        }
    }
}
