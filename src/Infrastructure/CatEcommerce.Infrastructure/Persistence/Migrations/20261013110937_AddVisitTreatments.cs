using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitTreatments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "visit_treatment",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    clinic_service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sku_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    stock_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    batch_expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    deducted_quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    shortfall_quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    dose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_billable = table.Column<bool>(type: "boolean", nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    unit_price_currency_code = table.Column<string>(type: "char(3)", nullable: true),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_override_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reversed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_treatment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visit_treatment_visit_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "clinic",
                        principalTable: "visit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_visit_treatment_batch_id",
                schema: "clinic",
                table: "visit_treatment",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_treatment_sku_id",
                schema: "clinic",
                table: "visit_treatment",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_treatment_visit_id",
                schema: "clinic",
                table: "visit_treatment",
                column: "visit_id");

            // HO-EMR-001 section 8.4 / ADR-118 -- the third immutability layer for treatment lines: no INSERT or UPDATE once the parent visit is
            // finalised (a reversal is an UPDATE of an OPEN visit's line), no DELETE ever, no TRUNCATE. The trigger functions are the ones
            // AddClinicalRecordImmutability created (visit_child_frozen: a child never changes visit_id; no_truncate).
            migrationBuilder.Sql("""
                ALTER TABLE clinic.visit_treatment ADD CONSTRAINT ck_visit_treatment_quantities
                    CHECK (quantity > 0 AND deducted_quantity >= 0 AND shortfall_quantity >= 0);

                CREATE TRIGGER tr_visit_treatment_frozen
                    BEFORE INSERT OR UPDATE OR DELETE ON clinic.visit_treatment
                    FOR EACH ROW EXECUTE FUNCTION clinic.visit_child_frozen();
                CREATE TRIGGER tr_visit_treatment_no_truncate BEFORE TRUNCATE ON clinic.visit_treatment
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_visit_treatment_no_truncate ON clinic.visit_treatment;
                DROP TRIGGER IF EXISTS tr_visit_treatment_frozen ON clinic.visit_treatment;
                """);

            migrationBuilder.DropTable(
                name: "visit_treatment",
                schema: "clinic");
        }
    }
}
