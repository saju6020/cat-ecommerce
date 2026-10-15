using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardingUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "boarding");

            migrationBuilder.CreateTable(
                name: "care_routine",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_care_routine", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rate_card",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_type = table.Column<int>(type: "integer", nullable: false),
                    nightly_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    weekly_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    monthly_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    additional_cat_nightly_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_rate_card", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unit",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit_type = table.Column<int>(type: "integer", nullable: false),
                    capacity_cats = table.Column<int>(type: "integer", nullable: false),
                    block = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_unit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unit_maintenance_block",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    occupied_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    occupied_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_unit_maintenance_block", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "care_routine_time",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    local_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    routine_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_routine_time", x => x.id);
                    table.ForeignKey(
                        name: "FK_care_routine_time_care_routine_routine_id",
                        column: x => x.routine_id,
                        principalSchema: "boarding",
                        principalTable: "care_routine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_care_routine_time_routine",
                schema: "boarding",
                table: "care_routine_time",
                column: "routine_id");

            migrationBuilder.CreateIndex(
                name: "ix_boarding_rate_card_type_effective",
                schema: "boarding",
                table: "rate_card",
                columns: new[] { "branch_id", "unit_type", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_boarding_unit_branch_code",
                schema: "boarding",
                table: "unit",
                columns: new[] { "branch_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_boarding_unit_branch_type",
                schema: "boarding",
                table: "unit",
                columns: new[] { "branch_id", "unit_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_unit_maintenance_block_branch_starts_on",
                schema: "boarding",
                table: "unit_maintenance_block",
                columns: new[] { "branch_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_unit_maintenance_block_unit_from",
                schema: "boarding",
                table: "unit_maintenance_block",
                columns: new[] { "unit_id", "occupied_from" });

            // ── Raw SQL EF cannot model (read before committing -- HO-BRD-001 section 17) ───────────────────────────
            // btree_gist makes `=` on a uuid usable inside a GiST exclusion constraint (HO-APT-001 ADR-068 proved the pattern, F28). Idempotent; Down keeps it.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // ADR-219 (renumbered ADR-352): two live maintenance blocks on one unit may not overlap. [) -- back-to-back blocks are fine.
            migrationBuilder.Sql("""
                ALTER TABLE boarding.unit_maintenance_block
                ADD CONSTRAINT ex_unit_maintenance
                EXCLUDE USING gist (
                    "unit_id" WITH =,
                    tstzrange("occupied_from", "occupied_to", '[)') WITH &&
                ) WHERE (NOT "is_cancelled");
                """);

            // C-ALL-1 / ADR-299 layer 1: every Forbidden table carries the retention trigger pair (U-A7 / I-A3). Literal calls: U-A7 scans for them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "unit"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "unit_maintenance_block"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "rate_card"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "care_routine"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "care_routine"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "rate_card"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "unit_maintenance_block"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "unit"));
            migrationBuilder.Sql("ALTER TABLE boarding.unit_maintenance_block DROP CONSTRAINT IF EXISTS ex_unit_maintenance;");

            migrationBuilder.DropTable(
                name: "care_routine_time",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "rate_card",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "unit",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "unit_maintenance_block",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "care_routine",
                schema: "boarding");
        }
    }
}
