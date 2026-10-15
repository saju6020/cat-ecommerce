using System;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDietAuthorisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "diet_authorisation",
                schema: "pharmacy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authorised_by = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expired_on = table.Column<DateOnly>(type: "date", nullable: true),
                    revoked_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    extension_count = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_diet_authorisation", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_diet_authorisation_active",
                schema: "pharmacy",
                table: "diet_authorisation",
                columns: new[] { "cat_id", "product_id" },
                unique: true,
                // Written FROM the enumeration, never a literal (HO-PHM-001 U-X3): at most one ACTIVE authorisation per cat and product.
                filter: $"status = {DietAuthorisationStatus.Active.Value}");

            migrationBuilder.CreateIndex(
                name: "ix_diet_authorisation_customer",
                schema: "pharmacy",
                table: "diet_authorisation",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_diet_authorisation_status_valid_to",
                schema: "pharmacy",
                table: "diet_authorisation",
                columns: new[] { "status", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "ix_diet_authorisation_visit",
                schema: "pharmacy",
                table: "diet_authorisation",
                column: "visit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diet_authorisation",
                schema: "pharmacy");
        }
    }
}
