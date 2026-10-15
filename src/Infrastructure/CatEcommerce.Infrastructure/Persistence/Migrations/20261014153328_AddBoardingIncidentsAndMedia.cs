using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardingIncidentsAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    immediate_action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    owner_notified = table.Column<bool>(type: "boolean", nullable: false),
                    owner_acknowledged_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    acknowledged_via = table.Column<int>(type: "integer", nullable: true),
                    escalated_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    phone_call_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    phone_call_by = table.Column<Guid>(type: "uuid", nullable: true),
                    phone_call_outcome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resolved_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_incident", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stay_media",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    captured_on_local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    hidden_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_stay_media", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incident_branch_status",
                schema: "boarding",
                table: "incident",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_escalation",
                schema: "boarding",
                table: "incident",
                columns: new[] { "severity", "owner_acknowledged_on", "escalated_on" },
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_incident_stay_reported",
                schema: "boarding",
                table: "incident",
                columns: new[] { "stay_id", "reported_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_media_file_id",
                schema: "boarding",
                table: "stay_media",
                column: "file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stay_media_purge",
                schema: "boarding",
                table: "stay_media",
                column: "uploaded_on",
                filter: "status <> 3 AND incident_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stay_media_stay_date",
                schema: "boarding",
                table: "stay_media",
                columns: new[] { "stay_id", "captured_on_local_date" });
            // C-ALL-1 / ADR-299 layer 1: every Forbidden table carries the retention trigger pair (U-A7 / I-A3). Literal calls: U-A7 scans for them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "incident"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay_media"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay_media"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "incident"));

            migrationBuilder.DropTable(
                name: "incident",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_media",
                schema: "boarding");
        }
    }
}
