using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "diagnostic_order",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ordered_by = table.Column<Guid>(type: "uuid", nullable: false),
                    ordered_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cancelled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_diagnostic_order", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "diagnostic_attachment",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    attached_by = table.Column<Guid>(type: "uuid", nullable: false),
                    attached_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    diagnostic_order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostic_attachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_diagnostic_attachment_diagnostic_order_diagnostic_order_id",
                        column: x => x.diagnostic_order_id,
                        principalSchema: "clinic",
                        principalTable: "diagnostic_order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diagnostic_requested_analyte",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    analyte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    diagnostic_order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostic_requested_analyte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_diagnostic_requested_analyte_diagnostic_order_diagnostic_or~",
                        column: x => x.diagnostic_order_id,
                        principalSchema: "clinic",
                        principalTable: "diagnostic_order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diagnostic_result_set",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_on = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supersedes_result_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    diagnostic_order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostic_result_set", x => x.Id);
                    table.ForeignKey(
                        name: "FK_diagnostic_result_set_diagnostic_order_diagnostic_order_id",
                        column: x => x.diagnostic_order_id,
                        principalSchema: "clinic",
                        principalTable: "diagnostic_order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diagnostic_result_value",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    analyte_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_kind = table.Column<int>(type: "integer", nullable: false),
                    numeric_value = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    text_value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    reference_low = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    reference_high = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    normal_text = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    flag = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    result_set_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostic_result_value", x => x.Id);
                    table.ForeignKey(
                        name: "FK_diagnostic_result_value_diagnostic_result_set_result_set_id",
                        column: x => x.result_set_id,
                        principalSchema: "clinic",
                        principalTable: "diagnostic_result_set",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_attachment_file_id",
                schema: "clinic",
                table: "diagnostic_attachment",
                column: "file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_attachment_order_id",
                schema: "clinic",
                table: "diagnostic_attachment",
                column: "diagnostic_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_order_branch_status",
                schema: "clinic",
                table: "diagnostic_order",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_order_cat_ordered_on",
                schema: "clinic",
                table: "diagnostic_order",
                columns: new[] { "cat_id", "ordered_on" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_order_visit_id",
                schema: "clinic",
                table: "diagnostic_order",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_requested_analyte_order_id",
                schema: "clinic",
                table: "diagnostic_requested_analyte",
                column: "diagnostic_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_result_set_order_id",
                schema: "clinic",
                table: "diagnostic_result_set",
                column: "diagnostic_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_result_value_result_set_id",
                schema: "clinic",
                table: "diagnostic_result_value",
                column: "result_set_id");

            // HO-EMR-001 §8.4 / ADR-118 -- results, values, requested analytes and attachments are append-only (a
            // correction is a new result set naming the one it supersedes); an order is never deleted; TRUNCATE is
            // refused everywhere. The functions were created by AddClinicalRecordImmutability.
            migrationBuilder.Sql("""
                CREATE TRIGGER tr_diagnostic_requested_analyte_append_only
                    BEFORE UPDATE OR DELETE ON clinic.diagnostic_requested_analyte
                    FOR EACH ROW EXECUTE FUNCTION clinic.append_only();
                CREATE TRIGGER tr_diagnostic_result_set_append_only
                    BEFORE UPDATE OR DELETE ON clinic.diagnostic_result_set
                    FOR EACH ROW EXECUTE FUNCTION clinic.append_only();
                CREATE TRIGGER tr_diagnostic_result_value_append_only
                    BEFORE UPDATE OR DELETE ON clinic.diagnostic_result_value
                    FOR EACH ROW EXECUTE FUNCTION clinic.append_only();
                CREATE TRIGGER tr_diagnostic_attachment_append_only
                    BEFORE UPDATE OR DELETE ON clinic.diagnostic_attachment
                    FOR EACH ROW EXECUTE FUNCTION clinic.append_only();
                CREATE TRIGGER tr_diagnostic_order_no_delete
                    BEFORE DELETE ON clinic.diagnostic_order
                    FOR EACH ROW EXECUTE FUNCTION clinic.no_delete();

                CREATE TRIGGER tr_diagnostic_order_no_truncate BEFORE TRUNCATE ON clinic.diagnostic_order
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_diagnostic_requested_analyte_no_truncate BEFORE TRUNCATE ON clinic.diagnostic_requested_analyte
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_diagnostic_result_set_no_truncate BEFORE TRUNCATE ON clinic.diagnostic_result_set
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_diagnostic_result_value_no_truncate BEFORE TRUNCATE ON clinic.diagnostic_result_value
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_diagnostic_attachment_no_truncate BEFORE TRUNCATE ON clinic.diagnostic_attachment
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();

                -- A stored file (HO-FIL-001, files.stored_file) that a clinical record references cannot be deleted
                -- out from under it: "no delete path exists for any clinical record or attached file" (§4.2). This
                -- deviates from §8.4's table, which listed files.stored_file as append-only: that table is HO-FIL-001's
                -- and is legitimately updated (erasure redaction) and purged (retention) for unreferenced files.
                -- AddConsents replaces this function to include consent forms.
                CREATE OR REPLACE FUNCTION clinic.stored_file_referenced() RETURNS trigger AS $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM clinic.visit_photo WHERE file_id = OLD."Id")
                       OR EXISTS (SELECT 1 FROM clinic.diagnostic_attachment WHERE file_id = OLD."Id") THEN
                        RAISE EXCEPTION 'files.stored_file is referenced by a clinical record and cannot be deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN OLD;
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_stored_file_referenced
                    BEFORE DELETE ON files.stored_file
                    FOR EACH ROW EXECUTE FUNCTION clinic.stored_file_referenced();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_stored_file_referenced ON files.stored_file;
                DROP FUNCTION IF EXISTS clinic.stored_file_referenced();
                """);

            migrationBuilder.DropTable(
                name: "diagnostic_attachment",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "diagnostic_requested_analyte",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "diagnostic_result_value",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "diagnostic_result_set",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "diagnostic_order",
                schema: "clinic");
        }
    }
}
