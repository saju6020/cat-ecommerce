using System;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyDispensing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispensing",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_number = table.Column<int>(type: "integer", nullable: false),
                    is_repeat = table.Column<bool>(type: "boolean", nullable: false),
                    repeat_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dispensed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    dispensed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    dispensed_on_local = table.Column<DateOnly>(type: "date", nullable: false),
                    witness_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fefo_override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    label_cat_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_medicine_line = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    label_dose_line = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    label_instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    label_dispensed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    label_expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    label_quantity_line = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_prescription_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label_prescriber_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_clinic_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_clinic_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label_clinic_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    label_controlled_notice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    label_standard_warnings = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    label_print_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reversed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_dispensing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dispensing_line",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    register_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    line_total = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    currency_code = table.Column<string>(type: "char(3)", nullable: true),
                    dispensing_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispensing_line", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispensing_line_dispensing_dispensing_id",
                        column: x => x.dispensing_id,
                        principalSchema: "pharmacy",
                        principalTable: "dispensing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dispensing_branch_dispensed_on",
                schema: "pharmacy",
                table: "dispensing",
                columns: new[] { "branch_id", "dispensed_on" });

            migrationBuilder.CreateIndex(
                name: "ix_dispensing_cat",
                schema: "pharmacy",
                table: "dispensing",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispensing_customer",
                schema: "pharmacy",
                table: "dispensing",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispensing_supply",
                schema: "pharmacy",
                table: "dispensing",
                columns: new[] { "prescription_id", "supply_number" },
                unique: true,
                // Written FROM the enumeration, never a literal (HO-PHM-001 U-X3): a supply number is used once among the supplies that STAND.
                filter: $"status = {DispensingStatus.Dispensed.Value}");

            migrationBuilder.CreateIndex(
                name: "ix_dispensing_line_batch",
                schema: "pharmacy",
                table: "dispensing_line",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispensing_line_dispensing",
                schema: "pharmacy",
                table: "dispensing_line",
                column: "dispensing_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispensing_line",
                schema: "pharmacy");

            migrationBuilder.DropTable(
                name: "dispensing",
                schema: "pharmacy");
        }
    }
}
