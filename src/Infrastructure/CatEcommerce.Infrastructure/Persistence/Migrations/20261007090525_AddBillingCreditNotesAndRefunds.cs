using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingCreditNotesAndRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credit_note",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    financial_year = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    net_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    released_to_customer_credit = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_credit_note", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refund",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_refund", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "credit_note_line",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    requested_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    net_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_rate_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    credit_note_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_note_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_note_line_credit_note_credit_note_id",
                        column: x => x.credit_note_id,
                        principalSchema: "billing",
                        principalTable: "credit_note",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_customer_issued_on",
                schema: "billing",
                table: "credit_note",
                columns: new[] { "customer_id", "issued_on" });

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_idempotency",
                schema: "billing",
                table: "credit_note",
                columns: new[] { "issued_by", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_invoice",
                schema: "billing",
                table: "credit_note",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_number",
                schema: "billing",
                table: "credit_note",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_sequence",
                schema: "billing",
                table: "credit_note",
                columns: new[] { "branch_id", "financial_year", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_note_line_credit_note_id",
                schema: "billing",
                table: "credit_note_line",
                column: "credit_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_line_invoice_line",
                schema: "billing",
                table: "credit_note_line",
                column: "invoice_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_branch_business_date",
                schema: "billing",
                table: "refund",
                columns: new[] { "branch_id", "business_date" });

            migrationBuilder.CreateIndex(
                name: "ix_refund_customer",
                schema: "billing",
                table: "refund",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_idempotency",
                schema: "billing",
                table: "refund",
                columns: new[] { "issued_by", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refund_payment",
                schema: "billing",
                table: "refund",
                column: "payment_id");

            foreach (var table in new[] { "credit_note", "credit_note_line", "refund" })
                migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.AppendOnly(table));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "credit_note", "credit_note_line", "refund" })
                migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropAppendOnly(table));

            migrationBuilder.DropTable(
                name: "credit_note_line",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "refund",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "credit_note",
                schema: "billing");
        }
    }
}
