using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalCatalogues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "diagnosis_code",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_system = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    external_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_diagnosis_code", x => x.Id);
                });

            // HO-EMR-001 §8.2 / ADR-123 -- the one system diagnosis code, so a well visit stays finalisable
            // (a visit needs at least one problem). Literal values on purpose: a seed is frozen history;
            // DiagnosisCodeSeed.RoutineNoAbnormality is asserted equal to these by a unit test.
            migrationBuilder.InsertData(
                schema: "clinic",
                table: "diagnosis_code",
                columns: new[]
                {
                    "Id", "code", "name", "category", "external_system", "external_code", "is_active",
                    "CreatedBy", "CreatedDate", "Language", "LastUpdatedDate", "LastUpdatedBy", "TenantId", "Tags",
                    "VerticalId", "ServiceId", "IsMarkedToDelete", "Version"
                },
                values: new object[]
                {
                    new Guid("5e1d0a6c-7b0e-4d0e-9a54-0c3a7c1b5a01"), "SYS-NAD", "Routine visit — no abnormality detected", null, null, null, true,
                    Guid.Empty, new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), Guid.Empty, Guid.Empty, null,
                    Guid.Empty, null, false, 1
                });

            migrationBuilder.CreateTable(
                name: "lab_analyte",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value_kind = table.Column<int>(type: "integer", nullable: false),
                    diagnostic_kind = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    reference_low = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    reference_high = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    normal_text = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_lab_analyte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "note_template",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subjective = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    objective = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    assessment = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    plan = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    discharge_instructions = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_note_template", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_diagnosis_code_code",
                schema: "clinic",
                table: "diagnosis_code",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lab_analyte_code",
                schema: "clinic",
                table: "lab_analyte",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_note_template_service_active",
                schema: "clinic",
                table: "note_template",
                column: "clinic_service_id",
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagnosis_code",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "lab_analyte",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "note_template",
                schema: "clinic");
        }
    }
}
