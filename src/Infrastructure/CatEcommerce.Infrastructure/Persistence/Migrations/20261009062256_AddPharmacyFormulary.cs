using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyFormulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pharmacy");

            migrationBuilder.CreateTable(
                name: "formulary_medicine",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    active_ingredients = table.Column<string[]>(type: "text[]", nullable: false),
                    drug_classes = table.Column<string[]>(type: "text[]", nullable: false),
                    form = table.Column<int>(type: "integer", nullable: false),
                    strength_amount = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    strength_unit = table.Column<int>(type: "integer", nullable: false),
                    strength_per_quantity = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    strength_per_unit = table.Column<int>(type: "integer", nullable: false),
                    default_route = table.Column<int>(type: "integer", nullable: false),
                    allowed_routes = table.Column<int[]>(type: "integer[]", nullable: false),
                    dosing_basis = table.Column<int>(type: "integer", nullable: false),
                    dose_min_mg_per_kg = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    dose_max_mg_per_kg = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    contraindications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_controlled = table.Column<bool>(type: "boolean", nullable: false),
                    controlled_schedule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    controlled_change_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dispensing_unit = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_formulary_medicine", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "formulary_interaction",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    interacts_with_medicine_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interacts_with_term = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    formulary_medicine_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formulary_interaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_formulary_interaction_formulary_medicine_formulary_medicine~",
                        column: x => x.formulary_medicine_id,
                        principalSchema: "pharmacy",
                        principalTable: "formulary_medicine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "formulary_medicine_sku",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    formulary_medicine_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formulary_medicine_sku", x => x.Id);
                    table.ForeignKey(
                        name: "FK_formulary_medicine_sku_formulary_medicine_formulary_medicin~",
                        column: x => x.formulary_medicine_id,
                        principalSchema: "pharmacy",
                        principalTable: "formulary_medicine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_formulary_interaction_medicine",
                schema: "pharmacy",
                table: "formulary_interaction",
                column: "formulary_medicine_id");

            migrationBuilder.CreateIndex(
                name: "ix_formulary_medicine_active_name",
                schema: "pharmacy",
                table: "formulary_medicine",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_formulary_medicine_code",
                schema: "pharmacy",
                table: "formulary_medicine",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_formulary_medicine_sku",
                schema: "pharmacy",
                table: "formulary_medicine_sku",
                column: "sku_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_formulary_medicine_sku_medicine",
                schema: "pharmacy",
                table: "formulary_medicine_sku",
                column: "formulary_medicine_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "formulary_interaction",
                schema: "pharmacy");

            migrationBuilder.DropTable(
                name: "formulary_medicine_sku",
                schema: "pharmacy");

            migrationBuilder.DropTable(
                name: "formulary_medicine",
                schema: "pharmacy");
        }
    }
}
