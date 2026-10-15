using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "visit",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<int>(type: "integer", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_category = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    opened_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    opened_by = table.Column<Guid>(type: "uuid", nullable: false),
                    subjective = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    objective = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    assessment = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    plan = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    discharge_instructions = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    note_saved_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    note_saved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_template_version = table.Column<int>(type: "integer", nullable: true),
                    follow_up_kind = table.Column<int>(type: "integer", nullable: true),
                    follow_up_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    follow_up_appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    follow_up_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    follow_up_set_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    follow_up_set_by = table.Column<Guid>(type: "uuid", nullable: true),
                    finalised_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    finalised_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_visit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "visit_photo",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taken_on = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    added_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_photo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visit_photo_visit_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "clinic",
                        principalTable: "visit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_problem",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnosis_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    certainty = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    added_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    withdrawn_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_problem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visit_problem_visit_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "clinic",
                        principalTable: "visit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_vitals",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    measured_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    temperature_c = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    heart_rate_bpm = table.Column<int>(type: "integer", nullable: true),
                    respiratory_rate_per_min = table.Column<int>(type: "integer", nullable: true),
                    body_condition_score = table.Column<int>(type: "integer", nullable: true),
                    pain_score = table.Column<int>(type: "integer", nullable: true),
                    confirmed_out_of_range = table.Column<int[]>(type: "integer[]", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_vitals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visit_vitals_visit_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "clinic",
                        principalTable: "visit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_visit_appointment",
                schema: "clinic",
                table: "visit",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visit_branch_status_opened_on",
                schema: "clinic",
                table: "visit",
                columns: new[] { "branch_id", "status", "opened_on" });

            migrationBuilder.CreateIndex(
                name: "ix_visit_cat_opened_on",
                schema: "clinic",
                table: "visit",
                columns: new[] { "cat_id", "opened_on" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_visit_photo_file_id",
                schema: "clinic",
                table: "visit_photo",
                column: "file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visit_photo_visit_id",
                schema: "clinic",
                table: "visit_photo",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_problem_diagnosis_code_id",
                schema: "clinic",
                table: "visit_problem",
                column: "diagnosis_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_problem_visit_id",
                schema: "clinic",
                table: "visit_problem",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_vitals_visit_id",
                schema: "clinic",
                table: "visit_vitals",
                column: "visit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "visit_photo",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "visit_problem",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "visit_vitals",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "visit",
                schema: "clinic");
        }
    }
}
