using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HO-BIL-001 §8.8 / §12.1 -- a receipt number is NOT a legal document number: gaps are allowed, so a
            // PostgreSQL sequence is the right tool here (and only here -- invoice and credit-note numbers never use one, ADR-179).
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS billing.receipt_number_seq AS bigint START WITH 1 INCREMENT BY 1 NO CYCLE;");

            migrationBuilder.CreateTable(
                name: "billing_profile",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_registration_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address_lines = table.Column<string[]>(type: "text[]", nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    invoice_footer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_billing_profile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_number_series",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_year = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    document_kind = table.Column<int>(type: "integer", nullable: false),
                    next_sequence = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_document_number_series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "billing_profile_bank_account",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    routing_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    billing_profile_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_profile_bank_account", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_profile_bank_account_billing_profile_billing_profil~",
                        column: x => x.billing_profile_id,
                        principalSchema: "billing",
                        principalTable: "billing_profile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_profile_bank_account_billing_profile_id",
                schema: "billing",
                table: "billing_profile_bank_account",
                column: "billing_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_number_series",
                schema: "billing",
                table: "document_number_series",
                columns: new[] { "branch_id", "financial_year", "document_kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_profile_bank_account",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "document_number_series",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "billing_profile",
                schema: "billing");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS billing.receipt_number_seq;");
        }
    }
}
