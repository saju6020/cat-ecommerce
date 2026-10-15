using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyPrescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HO-PHM-001 §8.3 -- RX-{yyyy}-{000001} comes from this sequence (IPrescriptionNumberAllocator). The unique index on the number is the backstop.
            migrationBuilder.Sql("CREATE SEQUENCE pharmacy.prescription_number_seq START WITH 1 INCREMENT BY 1 NO CYCLE;");

            migrationBuilder.CreateTable(
                name: "prescription",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_remote_renewal = table.Column<bool>(type: "boolean", nullable: false),
                    prescriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medicine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medicine_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    medicine_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    medicine_form = table.Column<int>(type: "integer", nullable: false),
                    medicine_strength_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    medicine_is_controlled = table.Column<bool>(type: "boolean", nullable: false),
                    medicine_controlled_schedule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    medicine_dispensing_unit = table.Column<int>(type: "integer", nullable: false),
                    medicine_linked_sku_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    dose_amount = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    dose_unit = table.Column<int>(type: "integer", nullable: false),
                    route = table.Column<int>(type: "integer", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_repeatable = table.Column<bool>(type: "boolean", nullable: false),
                    repeats_authorised = table.Column<int>(type: "integer", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    supplies_dispensed = table.Column<int>(type: "integer", nullable: false),
                    last_dispensed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cancelled_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    renewed_from_prescription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    renewed_by_prescription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    safety_weight_kg = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    safety_weight_recorded_on = table.Column<DateOnly>(type: "date", nullable: true),
                    safety_dose_mg_per_kg = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    safety_expected_min_mg_per_kg = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    safety_expected_max_mg_per_kg = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    safety_dose_check_outcome = table.Column<int>(type: "integer", nullable: false),
                    safety_warnings_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    safety_acknowledged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    safety_acknowledged_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    safety_allergy_ack_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    safety_dose_ack_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_prescription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prescription_warning",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    blocking = table.Column<bool>(type: "boolean", nullable: false),
                    requires_acknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: true),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prescription_warning", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prescription_warning_prescription_prescription_id",
                        column: x => x.prescription_id,
                        principalSchema: "pharmacy",
                        principalTable: "prescription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repeat_request",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    preferred_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decline_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dispensing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repeat_request", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repeat_request_prescription_prescription_id",
                        column: x => x.prescription_id,
                        principalSchema: "pharmacy",
                        principalTable: "prescription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prescription_branch_status_issued_on",
                schema: "pharmacy",
                table: "prescription",
                columns: new[] { "branch_id", "status", "issued_on" });

            migrationBuilder.CreateIndex(
                name: "ix_prescription_cat_status",
                schema: "pharmacy",
                table: "prescription",
                columns: new[] { "cat_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_prescription_customer",
                schema: "pharmacy",
                table: "prescription",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_number",
                schema: "pharmacy",
                table: "prescription",
                column: "prescription_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prescription_status_valid_until",
                schema: "pharmacy",
                table: "prescription",
                columns: new[] { "status", "valid_until" });

            migrationBuilder.CreateIndex(
                name: "ix_prescription_visit",
                schema: "pharmacy",
                table: "prescription",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_warning_prescription",
                schema: "pharmacy",
                table: "prescription_warning",
                column: "prescription_id");

            migrationBuilder.CreateIndex(
                name: "ix_repeat_request_prescription",
                schema: "pharmacy",
                table: "repeat_request",
                column: "prescription_id");

            migrationBuilder.CreateIndex(
                name: "ix_repeat_request_status_branch",
                schema: "pharmacy",
                table: "repeat_request",
                columns: new[] { "status", "preferred_branch_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS pharmacy.prescription_number_seq;");

            migrationBuilder.DropTable(
                name: "prescription_warning",
                schema: "pharmacy");

            migrationBuilder.DropTable(
                name: "repeat_request",
                schema: "pharmacy");

            migrationBuilder.DropTable(
                name: "prescription",
                schema: "pharmacy");
        }
    }
}
