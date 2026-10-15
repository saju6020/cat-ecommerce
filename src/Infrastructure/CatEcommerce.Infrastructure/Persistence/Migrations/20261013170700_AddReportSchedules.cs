using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "report");

            migrationBuilder.CreateTable(
                name: "report_schedule",
                schema: "report",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    report_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    period = table.Column<int>(type: "integer", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: true),
                    day_of_month = table.Column<int>(type: "integer", nullable: true),
                    last_day_of_month = table.Column<bool>(type: "boolean", nullable: false),
                    send_at_local = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    recipient_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    next_run_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_run_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_run_due_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_report_schedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_report_run",
                schema: "report",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    trigger = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content_length = table.Column<long>(type: "bigint", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    columns_included = table.Column<string[]>(type: "text[]", nullable: false),
                    attachment_omitted = table.Column<bool>(type: "boolean", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    generated_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    purged_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_scheduled_report_run", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_report_schedule_status_next_run_on",
                schema: "report",
                table: "report_schedule",
                columns: new[] { "status", "next_run_on" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_report_run_schedule_generated_on",
                schema: "report",
                table: "scheduled_report_run",
                columns: new[] { "schedule_id", "generated_on" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_report_run_slot",
                schema: "report",
                table: "scheduled_report_run",
                columns: new[] { "schedule_id", "due_on" },
                unique: true,
                filter: "\"trigger\" = 1 AND status IN (1, 3)");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_report_run_status_generated_on",
                schema: "report",
                table: "scheduled_report_run",
                columns: new[] { "status", "generated_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_schedule",
                schema: "report");

            migrationBuilder.DropTable(
                name: "scheduled_report_run",
                schema: "report");
        }
    }
}
