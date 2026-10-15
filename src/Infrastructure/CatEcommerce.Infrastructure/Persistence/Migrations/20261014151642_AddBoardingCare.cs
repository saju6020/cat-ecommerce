using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardingCare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "check_in_health_observation",
                schema: "boarding",
                table: "stay_cat",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "check_in_identity_confirmed",
                schema: "boarding",
                table: "stay_cat",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "check_in_signature_method",
                schema: "boarding",
                table: "stay_cat",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_in_signature_strokes",
                schema: "boarding",
                table: "stay_cat",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "check_in_signed_on",
                schema: "boarding",
                table: "stay_cat",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_in_signer_name",
                schema: "boarding",
                table: "stay_cat",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "check_in_vaccination_proof_verified",
                schema: "boarding",
                table: "stay_cat",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "check_in_weight_kg",
                schema: "boarding",
                table: "stay_cat",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "check_out_signature_method",
                schema: "boarding",
                table: "stay_cat",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_out_signature_strokes",
                schema: "boarding",
                table: "stay_cat",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "check_out_signed_on",
                schema: "boarding",
                table: "stay_cat",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_out_signer_name",
                schema: "boarding",
                table: "stay_cat",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "check_out_weight_kg",
                schema: "boarding",
                table: "stay_cat",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_in_on",
                schema: "boarding",
                table: "stay_cat",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_out_on",
                schema: "boarding",
                table: "stay_cat",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "infectious_flagged_by",
                schema: "boarding",
                table: "stay_cat",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "infectious_flagged_on",
                schema: "boarding",
                table: "stay_cat",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "infectious_note",
                schema: "boarding",
                table: "stay_cat",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "no_belongings",
                schema: "boarding",
                table: "stay_cat",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "suspected_infectious",
                schema: "boarding",
                table: "stay_cat",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "care_observation",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    portion_eaten = table.Column<int>(type: "integer", nullable: false),
                    water_intake = table.Column<int>(type: "integer", nullable: false),
                    litter_output = table.Column<int>(type: "integer", nullable: false),
                    mood = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_care_observation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "care_task",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    due_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    due_local_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    source_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assigned_caretaker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    skip_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_care_task", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "isolation_entry",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entered_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    left_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    purpose = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_isolation_entry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stay_cat_belonging",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    returned = table.Column<bool>(type: "boolean", nullable: true),
                    return_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stay_cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_cat_belonging", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_cat_belonging_stay_cat_stay_cat_id",
                        column: x => x.stay_cat_id,
                        principalSchema: "boarding",
                        principalTable: "stay_cat",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_care_observation_stay_cat_recorded",
                schema: "boarding",
                table: "care_observation",
                columns: new[] { "stay_id", "cat_id", "recorded_on" });

            migrationBuilder.CreateIndex(
                name: "ix_care_task_branch_date_status",
                schema: "boarding",
                table: "care_task",
                columns: new[] { "branch_id", "local_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_care_task_caretaker_date",
                schema: "boarding",
                table: "care_task",
                columns: new[] { "assigned_caretaker_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ix_care_task_generation",
                schema: "boarding",
                table: "care_task",
                columns: new[] { "stay_id", "cat_id", "local_date", "source_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_isolation_entry_stay_entered",
                schema: "boarding",
                table: "isolation_entry",
                columns: new[] { "stay_id", "entered_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_cat_belonging_stay_cat",
                schema: "boarding",
                table: "stay_cat_belonging",
                column: "stay_cat_id");

            // C-ALL-1 / ADR-299 layer 1: every Forbidden table carries the retention trigger pair (U-A7 / I-A3). Literal calls: U-A7 scans for them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "care_task"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "care_observation"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "isolation_entry"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "isolation_entry"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "care_observation"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "care_task"));

            migrationBuilder.DropTable(
                name: "care_observation",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "care_task",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "isolation_entry",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_cat_belonging",
                schema: "boarding");

            migrationBuilder.DropColumn(
                name: "check_in_health_observation",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_in_identity_confirmed",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_in_signature_method",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_in_signature_strokes",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_in_signed_on",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_in_signer_name",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_in_vaccination_proof_verified",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_in_weight_kg",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_out_signature_method",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_out_signature_strokes",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_out_signed_on",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_out_signer_name",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "check_out_weight_kg",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "checked_in_on",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "checked_out_on",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "infectious_flagged_by",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "infectious_flagged_on",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "infectious_note",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "no_belongings",
                schema: "boarding",
                table: "stay_cat");

            migrationBuilder.DropColumn(
                name: "suspected_infectious",
                schema: "boarding",
                table: "stay_cat");
        }
    }
}
