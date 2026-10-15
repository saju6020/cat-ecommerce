using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_match",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    matched_by = table.Column<Guid>(type: "uuid", nullable: false),
                    matched_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    unmatched_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    unmatched_by = table.Column<Guid>(type: "uuid", nullable: true),
                    unmatch_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_bank_match", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    source_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    imported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    imported_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_bank_statement", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cod_remittance",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    courier_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_cod_remittance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement_line",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    bank_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    statement_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_statement_line_bank_statement_statement_id",
                        column: x => x.statement_id,
                        principalSchema: "billing",
                        principalTable: "bank_statement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cod_remittance_line",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_remitted = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    remittance_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cod_remittance_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_cod_remittance_line_cod_remittance_remittance_id",
                        column: x => x.remittance_id,
                        principalSchema: "billing",
                        principalTable: "cod_remittance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_match_line",
                schema: "billing",
                table: "bank_match",
                column: "statement_line_id",
                unique: true,
                filter: "unmatched_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bank_match_payment",
                schema: "billing",
                table: "bank_match",
                column: "payment_id",
                unique: true,
                filter: "unmatched_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bank_match_statement",
                schema: "billing",
                table: "bank_match",
                column: "statement_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_statement_branch_imported_on",
                schema: "billing",
                table: "bank_statement",
                columns: new[] { "branch_id", "imported_on" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_statement_source",
                schema: "billing",
                table: "bank_statement",
                columns: new[] { "bank_account_label", "source_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_statement_line_date",
                schema: "billing",
                table: "bank_statement_line",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_line_statement_id",
                schema: "billing",
                table: "bank_statement_line",
                column: "statement_id");

            migrationBuilder.CreateIndex(
                name: "ix_cod_remittance_branch_courier",
                schema: "billing",
                table: "cod_remittance",
                columns: new[] { "branch_id", "courier_code", "received_on" });

            migrationBuilder.CreateIndex(
                name: "ix_cod_remittance_line_payment",
                schema: "billing",
                table: "cod_remittance_line",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_cod_remittance_line_remittance_id",
                schema: "billing",
                table: "cod_remittance_line",
                column: "remittance_id");

            foreach (var table in new[] { "cod_remittance", "cod_remittance_line", "bank_statement", "bank_statement_line" })
                migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.AppendOnly(table));

            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.AppendOnly("bank_match", "unmatched_on", "unmatched_by", "unmatch_reason"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropAppendOnly("bank_match"));

            foreach (var table in new[] { "cod_remittance", "cod_remittance_line", "bank_statement", "bank_statement_line" })
                migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropAppendOnly(table));

            migrationBuilder.DropTable(
                name: "bank_match",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "bank_statement_line",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "cod_remittance_line",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "bank_statement",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "cod_remittance",
                schema: "billing");
        }
    }
}
