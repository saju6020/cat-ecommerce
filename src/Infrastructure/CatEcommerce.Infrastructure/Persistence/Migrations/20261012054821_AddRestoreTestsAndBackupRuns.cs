using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestoreTestsAndBackupRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_run",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    started_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    finished_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    repository = table.Column<string>(type: "text", nullable: false),
                    backup_label = table.Column<string>(type: "text", nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    wal_archived_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    error_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "restore_test_record",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    performed_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    backup_set_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    simulated_failure_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    recovered_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    restore_started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    restore_completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    achieved_rpo_minutes = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    achieved_rto_minutes = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    drill_output = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_restore_test_record", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "restore_check_result",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    restore_test_record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restore_check_result", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restore_check_result_restore_test_record_restore_test_recor~",
                        column: x => x.restore_test_record_id,
                        principalSchema: "ops",
                        principalTable: "restore_test_record",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_backup_run_kind_finished_on",
                schema: "ops",
                table: "backup_run",
                columns: new[] { "kind", "finished_on" });

            migrationBuilder.CreateIndex(
                name: "IX_restore_check_result_restore_test_record_id",
                schema: "ops",
                table: "restore_check_result",
                column: "restore_test_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_restore_test_record_outcome_performed_on",
                schema: "ops",
                table: "restore_test_record",
                columns: new[] { "outcome", "performed_on" });

            migrationBuilder.CreateIndex(
                name: "ix_restore_test_record_performed_on",
                schema: "ops",
                table: "restore_test_record",
                column: "performed_on");

            // C-ALL-1: both restore-drill tables are Audit and Forbidden, so they get the two retention triggers in the migration that creates them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("ops", "restore_test_record"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("ops", "restore_check_result"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("ops", "restore_check_result"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("ops", "restore_test_record"));

            migrationBuilder.DropTable(
                name: "backup_run",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "restore_check_result",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "restore_test_record",
                schema: "ops");
        }
    }
}
