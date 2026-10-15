using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnaesthesiaLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anaesthesia_log_entry",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    surgery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    logged_by = table.Column<Guid>(type: "uuid", nullable: false),
                    logged_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    medicine_id = table.Column<Guid>(type: "uuid", nullable: true),
                    medicine_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_controlled = table.Column<bool>(type: "boolean", nullable: false),
                    dose_amount = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    dose_unit = table.Column<int>(type: "integer", nullable: true),
                    route = table.Column<int>(type: "integer", nullable: true),
                    administered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity_drawn = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    quantity_wasted = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    stock_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    witness_id = table.Column<Guid>(type: "uuid", nullable: true),
                    register_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_shortfall = table.Column<bool>(type: "boolean", nullable: false),
                    heart_rate_bpm = table.Column<int>(type: "integer", nullable: true),
                    respiratory_rate_per_min = table.Column<int>(type: "integer", nullable: true),
                    spo2_percent = table.Column<int>(type: "integer", nullable: true),
                    etco2_mmhg = table.Column<int>(type: "integer", nullable: true),
                    temperature_celsius = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    systolic_mmhg = table.Column<int>(type: "integer", nullable: true),
                    mean_arterial_mmhg = table.Column<int>(type: "integer", nullable: true),
                    implausible_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    milestone = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    corrects_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corrected_kind = table.Column<int>(type: "integer", nullable: true),
                    correction_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_anaesthesia_log_entry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anaesthesia_log_entry_corrects",
                schema: "clinic",
                table: "anaesthesia_log_entry",
                column: "corrects_entry_id",
                filter: "corrects_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_anaesthesia_log_entry_timeline",
                schema: "clinic",
                table: "anaesthesia_log_entry",
                columns: new[] { "surgery_id", "occurred_on", "logged_on" });

            // HO-SRG-001 section 8.8 (ADR-162): the anaesthesia log is APPEND-ONLY. An entry row is inserted once; nothing may change or remove it -- not an endpoint (there is none),
            // not a handler, not a data fix. This trigger is the second line behind the aggregate, and catches the code path nobody intended. The message starts "immutable:" so the
            // Api filter can map P0001 to 409 RecordIsImmutable (F31); any other P0001 is a defect and is rethrown.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION clinic.refuse_anaesthesia_log_change() RETURNS trigger AS $body$
                BEGIN
                    RAISE EXCEPTION 'immutable: anaesthesia log entries cannot be changed' USING ERRCODE = 'P0001';
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_anaesthesia_log_entry_immutable
                    BEFORE UPDATE OR DELETE ON clinic.anaesthesia_log_entry
                    FOR EACH ROW EXECUTE FUNCTION clinic.refuse_anaesthesia_log_change();

                CREATE TRIGGER trg_anaesthesia_log_entry_no_truncate
                    BEFORE TRUNCATE ON clinic.anaesthesia_log_entry
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.refuse_anaesthesia_log_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Triggers first, then the table (a Down is a schema change, never a data fix of immutable rows -- the table is dropped whole).
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_anaesthesia_log_entry_immutable ON clinic.anaesthesia_log_entry;
                DROP TRIGGER IF EXISTS trg_anaesthesia_log_entry_no_truncate ON clinic.anaesthesia_log_entry;
                DROP FUNCTION IF EXISTS clinic.refuse_anaesthesia_log_change();
                """);

            migrationBuilder.DropTable(
                name: "anaesthesia_log_entry",
                schema: "clinic");
        }
    }
}
