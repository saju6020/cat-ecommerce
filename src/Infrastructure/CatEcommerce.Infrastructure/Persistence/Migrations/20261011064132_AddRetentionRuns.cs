using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retention_run",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger = table.Column<int>(type: "integer", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    started_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    finished_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_retention_run", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retention_pass_result",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    cutoff = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    examined = table.Column<int>(type: "integer", nullable: false),
                    affected = table.Column<int>(type: "integer", nullable: false),
                    held = table.Column<int>(type: "integer", nullable: false),
                    failed = table.Column<int>(type: "integer", nullable: false),
                    error_codes = table.Column<string[]>(type: "text[]", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    retention_run_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_pass_result", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retention_pass_result_retention_run_retention_run_id",
                        column: x => x.retention_run_id,
                        principalSchema: "ops",
                        principalTable: "retention_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_retention_pass_result_retention_run_id",
                schema: "ops",
                table: "retention_pass_result",
                column: "retention_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_retention_run_requested_on",
                schema: "ops",
                table: "retention_run",
                column: "requested_on");

            migrationBuilder.CreateIndex(
                name: "ix_retention_run_status",
                schema: "ops",
                table: "retention_run",
                column: "status");

            // C-ALL-1: both ops tables are Audit and Forbidden, so they get the two retention triggers in the migration that creates them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("ops", "retention_run"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("ops", "retention_pass_result"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("ops", "retention_pass_result"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("ops", "retention_run"));

            migrationBuilder.DropTable(
                name: "retention_pass_result",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "retention_run",
                schema: "ops");
        }
    }
}
