using System;
using CatEcommerce.Infrastructure.Pharmacy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddControlledDrugRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HO-PHM-001 §8.9 / V1 -- the register's entries are numbered from this sequence (the column default); it must exist before the table.
            migrationBuilder.Sql(ControlledDrugRegisterSql.CreateEntryNumberSequence());

            migrationBuilder.CreateTable(
                name: "cd_register_entry",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('pharmacy.cd_register_entry_number_seq')"),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medicine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medicine_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    medicine_strength_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    medicine_form = table.Column<int>(type: "integer", nullable: false),
                    medicine_controlled_schedule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    medicine_dispensing_unit = table.Column<int>(type: "integer", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    batch_expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    dose_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    wastage_quantity = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    witness_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corrects_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correction_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    returned_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_cd_register_entry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cd_register_seal",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_position = table.Column<long>(type: "bigint", nullable: false),
                    previous_hash = table.Column<string>(type: "char(64)", nullable: false),
                    entry_hash = table.Column<string>(type: "char(64)", nullable: false),
                    sealed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_cd_register_seal", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cd_register_entry_branch_batch",
                schema: "pharmacy",
                table: "cd_register_entry",
                columns: new[] { "branch_id", "batch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cd_register_entry_branch_medicine",
                schema: "pharmacy",
                table: "cd_register_entry",
                columns: new[] { "branch_id", "medicine_id", "entry_number" });

            migrationBuilder.CreateIndex(
                name: "ix_cd_register_entry_branch_recorded_on",
                schema: "pharmacy",
                table: "cd_register_entry",
                columns: new[] { "branch_id", "recorded_on" });

            migrationBuilder.CreateIndex(
                name: "ix_cd_register_entry_corrects",
                schema: "pharmacy",
                table: "cd_register_entry",
                column: "corrects_entry_id",
                filter: "corrects_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cd_register_entry_number",
                schema: "pharmacy",
                table: "cd_register_entry",
                column: "entry_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cd_register_entry_source",
                schema: "pharmacy",
                table: "cd_register_entry",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cd_register_seal_chain",
                schema: "pharmacy",
                table: "cd_register_seal",
                columns: new[] { "branch_id", "chain_position" },
                unique: true);

            // HO-PHM-001 §8.9 layer 3 (ADR-144, V2) -- the four triggers that refuse UPDATE, DELETE and TRUNCATE of the register and its seals.
            migrationBuilder.Sql(ControlledDrugRegisterSql.CreateImmutabilityTriggers());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NEVER run this against a database holding real register entries (HO-PHM-001 §20.8): it drops the triggers that protect them, and then the tables.
            migrationBuilder.Sql(ControlledDrugRegisterSql.DropImmutabilityTriggers());

            migrationBuilder.DropTable(
                name: "cd_register_entry",
                schema: "pharmacy");

            migrationBuilder.DropTable(
                name: "cd_register_seal",
                schema: "pharmacy");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS pharmacy.cd_register_entry_number_seq;");
        }
    }
}