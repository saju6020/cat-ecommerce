using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryConsignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consignment",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    shipment_reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    purchase_order_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    import_permit_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    health_certificate_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    customs_declaration_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    expected_arrival_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cleared_on = table.Column<DateOnly>(type: "date", nullable: true),
                    allocated_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    allocated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    costs_changed_since_allocation = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_consignment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "consignment_cost",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_cost", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_cost_consignment_consignment_id",
                        column: x => x.consignment_id,
                        principalSchema: "inventory",
                        principalTable: "consignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consignment_document",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    document_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_in_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    superseded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    superseded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_document", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_document_consignment_consignment_id",
                        column: x => x.consignment_id,
                        principalSchema: "inventory",
                        principalTable: "consignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consignment_exchange_rate",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    rate_to_base = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    recorded_on = table.Column<DateOnly>(type: "date", nullable: false),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_exchange_rate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_exchange_rate_consignment_consignment_id",
                        column: x => x.consignment_id,
                        principalSchema: "inventory",
                        principalTable: "consignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consignment_hs_code",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_hs_code", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_hs_code_consignment_consignment_id",
                        column: x => x.consignment_id,
                        principalSchema: "inventory",
                        principalTable: "consignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consignment_status_change",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: true),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_status_change", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_status_change_consignment_consignment_id",
                        column: x => x.consignment_id,
                        principalSchema: "inventory",
                        principalTable: "consignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consignment_branch_status",
                schema: "inventory",
                table: "consignment",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_consignment_reference",
                schema: "inventory",
                table: "consignment",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignment_cost_consignment_id",
                schema: "inventory",
                table: "consignment_cost",
                column: "consignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_document_consignment_id",
                schema: "inventory",
                table: "consignment_document",
                column: "consignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_consignment_exchange_rate_currency",
                schema: "inventory",
                table: "consignment_exchange_rate",
                columns: new[] { "consignment_id", "currency_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignment_hs_code_consignment_id",
                schema: "inventory",
                table: "consignment_hs_code",
                column: "consignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_status_change_consignment_id",
                schema: "inventory",
                table: "consignment_status_change",
                column: "consignment_id");

            // HO-IMP-001 ADR-114 -- CN-{yyyy}-{seq:D6}; not gapless.
            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS inventory.consignment_number_seq START WITH 1 INCREMENT BY 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consignment_cost",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "consignment_document",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "consignment_exchange_rate",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "consignment_hs_code",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "consignment_status_change",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "consignment",
                schema: "inventory");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS inventory.consignment_number_seq;");
        }
    }
}
