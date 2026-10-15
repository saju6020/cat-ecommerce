using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    financial_year = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sequence_number = table.Column<long>(type: "bigint", nullable: true),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    seller_legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    seller_tax_registration_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    seller_address_text = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: true),
                    seller_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    seller_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    seller_invoice_footer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    seller_bank_accounts_json = table.Column<string>(type: "text", nullable: true),
                    bill_to_full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bill_to_mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    bill_to_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bill_to_address_text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    net_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    paid_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    credited_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    loyalty_points = table.Column<int>(type: "integer", nullable: true),
                    loyalty_value = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    loyalty_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corrects_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corrects_credit_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    drafted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: true),
                    issue_idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_invoice", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_line",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corrects_invoice_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    business_line = table.Column<int>(type: "integer", nullable: false),
                    item_kind = table.Column<int>(type: "integer", nullable: false),
                    source_kind = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_group_kind = table.Column<int>(type: "integer", nullable: true),
                    billing_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    line_discount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    invoice_discount_share = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_class_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tax_rate_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    invoice_discarded = table.Column<bool>(type: "boolean", nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_line_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "billing",
                        principalTable: "invoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_branch_issue_date",
                schema: "billing",
                table: "invoice",
                columns: new[] { "branch_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_corrects_invoice",
                schema: "billing",
                table: "invoice",
                column: "corrects_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_customer_status",
                schema: "billing",
                table: "invoice",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_idempotency",
                schema: "billing",
                table: "invoice",
                columns: new[] { "issued_by", "issue_idempotency_key" },
                unique: true,
                filter: "issue_idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_number",
                schema: "billing",
                table: "invoice",
                column: "number",
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_sequence",
                schema: "billing",
                table: "invoice",
                columns: new[] { "branch_id", "financial_year", "sequence_number" },
                unique: true,
                filter: "sequence_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_status_due_on",
                schema: "billing",
                table: "invoice",
                columns: new[] { "status", "due_on" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_line_charge",
                schema: "billing",
                table: "invoice_line",
                column: "charge_id",
                unique: true,
                filter: "charge_id IS NOT NULL AND NOT invoice_discarded");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_line_invoice_id",
                schema: "billing",
                table: "invoice_line",
                column: "invoice_id");

            // ADR-180: issued invoices and their lines are immutable in the database too -- hand-written SQL, read before
            // commit; the status values come from InvoiceStatus.X.Value (see BillingMigrationSql, U-X3).
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.InvoiceImmutability());

            // D-6: nothing the Billing foundation or the charges recorded is ever deleted (a charge is voided, a profile is replaced).
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.NoDelete("billing_profile"));
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.NoDelete("document_number_series"));
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.NoDelete("charge"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropInvoiceImmutability());
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropNoDelete("charge"));
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropNoDelete("document_number_series"));
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropNoDelete("billing_profile"));

            migrationBuilder.DropTable(
                name: "invoice_line",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "invoice",
                schema: "billing");
        }
    }
}
