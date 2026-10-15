using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSurgery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "surgery",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    procedure_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    procedure_category = table.Column<int>(type: "integer", nullable: false),
                    procedure_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    procedure_is_sterilisation = table.Column<bool>(type: "boolean", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    price_currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    price_basis = table.Column<int>(type: "integer", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: true),
                    campaign_eligibility_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    theatre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    surgeon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assistant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ends_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    admit_by = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fasting_instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    schedule_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    asa_grade = table.Column<int>(type: "integer", nullable: true),
                    asa_is_emergency = table.Column<bool>(type: "boolean", nullable: false),
                    consent_override_by = table.Column<Guid>(type: "uuid", nullable: true),
                    consent_override_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    consent_override_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    admitted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    procedure_started_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    procedure_ended_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    procedure_ended_local_date = table.Column<DateOnly>(type: "date", nullable: true),
                    discharged_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    outcome = table.Column<int>(type: "integer", nullable: true),
                    recovery_status = table.Column<int>(type: "integer", nullable: true),
                    post_op_instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    cat_was_deceased_at_discharge = table.Column<bool>(type: "boolean", nullable: false),
                    cancelled_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_surgery", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "surgery_checklist_item",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    waiver_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: true),
                    surgery_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surgery_checklist_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_surgery_checklist_item_surgery_surgery_id",
                        column: x => x.surgery_id,
                        principalSchema: "clinic",
                        principalTable: "surgery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "surgery_follow_up",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    window_from = table.Column<DateOnly>(type: "date", nullable: false),
                    window_to = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    closed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    surgery_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surgery_follow_up", x => x.id);
                    table.ForeignKey(
                        name: "FK_surgery_follow_up_surgery_surgery_id",
                        column: x => x.surgery_id,
                        principalSchema: "clinic",
                        principalTable: "surgery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "surgery_recovery_change",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: true),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    surgery_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surgery_recovery_change", x => x.id);
                    table.ForeignKey(
                        name: "FK_surgery_recovery_change_surgery_surgery_id",
                        column: x => x.surgery_id,
                        principalSchema: "clinic",
                        principalTable: "surgery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "surgery_status_change",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: true),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    surgery_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surgery_status_change", x => x.id);
                    table.ForeignKey(
                        name: "FK_surgery_status_change_surgery_surgery_id",
                        column: x => x.surgery_id,
                        principalSchema: "clinic",
                        principalTable: "surgery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_surgery_appointment_id",
                schema: "clinic",
                table: "surgery",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_surgery_branch_starts_on",
                schema: "clinic",
                table: "surgery",
                columns: new[] { "branch_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_surgery_cat_starts_on",
                schema: "clinic",
                table: "surgery",
                columns: new[] { "cat_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_surgery_customer",
                schema: "clinic",
                table: "surgery",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_surgery_status_branch",
                schema: "clinic",
                table: "surgery",
                columns: new[] { "status", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_surgery_checklist_item_surgery",
                schema: "clinic",
                table: "surgery_checklist_item",
                column: "surgery_id");

            migrationBuilder.CreateIndex(
                name: "ix_surgery_follow_up_surgery",
                schema: "clinic",
                table: "surgery_follow_up",
                column: "surgery_id");

            migrationBuilder.CreateIndex(
                name: "ix_surgery_recovery_change_surgery",
                schema: "clinic",
                table: "surgery_recovery_change",
                column: "surgery_id");

            migrationBuilder.CreateIndex(
                name: "ix_surgery_status_change_surgery",
                schema: "clinic",
                table: "surgery_status_change",
                column: "surgery_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "surgery_checklist_item",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "surgery_follow_up",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "surgery_recovery_change",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "surgery_status_change",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "surgery",
                schema: "clinic");
        }
    }
}
