using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSurgicalReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "surgical_report",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    surgery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    procedure_performed = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    findings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    technique = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    suture_material = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    had_complications = table.Column<bool>(type: "boolean", nullable: false),
                    complications = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    surgeon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assistant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_surgical_report", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "surgical_report_addendum",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: false),
                    added_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surgical_report_addendum", x => x.id);
                    table.ForeignKey(
                        name: "FK_surgical_report_addendum_surgical_report_report_id",
                        column: x => x.report_id,
                        principalSchema: "clinic",
                        principalTable: "surgical_report",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_surgical_report_cat",
                schema: "clinic",
                table: "surgical_report",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "ix_surgical_report_addendum_report",
                schema: "clinic",
                table: "surgical_report_addendum",
                column: "report_id");

            // HO-SRG-001 section 8.2 / ADR-165: a FINALISED surgical report is immutable -- by the aggregate, and here. A report row can never be updated once finalised_on is set (finalising
            // itself updates a DRAFT, so it passes) and no report row is ever deleted; an addendum row can never be updated or deleted. The messages start "immutable:" so the Api filter can map
            // P0001 to 409 RecordIsImmutable (F31). Adding an addendum only INSERTs into the addendum table -- the report row is never touched.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION clinic.refuse_surgical_report_change() RETURNS trigger AS $body$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'immutable: surgical reports cannot be deleted' USING ERRCODE = 'P0001';
                    END IF;

                    IF OLD.finalised_on IS NOT NULL THEN
                        RAISE EXCEPTION 'immutable: a finalised surgical report cannot be changed; add an addendum' USING ERRCODE = 'P0001';
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_surgical_report_immutable
                    BEFORE UPDATE OR DELETE ON clinic.surgical_report
                    FOR EACH ROW EXECUTE FUNCTION clinic.refuse_surgical_report_change();

                CREATE OR REPLACE FUNCTION clinic.refuse_surgical_report_addendum_change() RETURNS trigger AS $body$
                BEGIN
                    RAISE EXCEPTION 'immutable: surgical report addenda cannot be changed' USING ERRCODE = 'P0001';
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_surgical_report_addendum_immutable
                    BEFORE UPDATE OR DELETE ON clinic.surgical_report_addendum
                    FOR EACH ROW EXECUTE FUNCTION clinic.refuse_surgical_report_addendum_change();

                CREATE TRIGGER trg_surgical_report_no_truncate
                    BEFORE TRUNCATE ON clinic.surgical_report
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.refuse_surgical_report_addendum_change();

                CREATE TRIGGER trg_surgical_report_addendum_no_truncate
                    BEFORE TRUNCATE ON clinic.surgical_report_addendum
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.refuse_surgical_report_addendum_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Triggers first, then the tables (a Down is a schema change; the tables are dropped whole, never row by row).
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_surgical_report_immutable ON clinic.surgical_report;
                DROP TRIGGER IF EXISTS trg_surgical_report_no_truncate ON clinic.surgical_report;
                DROP TRIGGER IF EXISTS trg_surgical_report_addendum_immutable ON clinic.surgical_report_addendum;
                DROP TRIGGER IF EXISTS trg_surgical_report_addendum_no_truncate ON clinic.surgical_report_addendum;
                DROP FUNCTION IF EXISTS clinic.refuse_surgical_report_change();
                DROP FUNCTION IF EXISTS clinic.refuse_surgical_report_addendum_change();
                """);

            migrationBuilder.DropTable(
                name: "surgical_report_addendum",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "surgical_report",
                schema: "clinic");
        }
    }
}
