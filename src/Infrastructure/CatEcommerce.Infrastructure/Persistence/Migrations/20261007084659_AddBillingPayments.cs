using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    external_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    collector_kind = table.Column<int>(type: "integer", nullable: true),
                    collector_staff_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    collector_courier_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    collector_courier_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    earmark_kind = table.Column<int>(type: "integer", nullable: true),
                    earmark_source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    earmark_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    earmark_released_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    earmark_released_by = table.Column<Guid>(type: "uuid", nullable: true),
                    earmark_release_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    accepted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    business_date = table.Column<DateOnly>(type: "date", nullable: true),
                    receipt_number = table.Column<long>(type: "bigint", nullable: true, defaultValueSql: "nextval('billing.receipt_number_seq')"),
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reversal_business_date = table.Column<DateOnly>(type: "date", nullable: true),
                    refunded_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
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
                    table.PrimaryKey("PK_payment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocation_entry",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    release_reason = table.Column<int>(type: "integer", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocation_entry", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_allocation_entry_payment_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "billing",
                        principalTable: "payment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_requested_allocation",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_requested_allocation", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_requested_allocation_payment_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "billing",
                        principalTable: "payment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_branch_business_date",
                schema: "billing",
                table: "payment",
                columns: new[] { "branch_id", "business_date" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_customer_status",
                schema: "billing",
                table: "payment",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_earmark",
                schema: "billing",
                table: "payment",
                columns: new[] { "earmark_kind", "earmark_source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_idempotency",
                schema: "billing",
                table: "payment",
                columns: new[] { "recorded_by", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_method_status_received_on",
                schema: "billing",
                table: "payment",
                columns: new[] { "method", "status", "received_on" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_status_recorded_on",
                schema: "billing",
                table: "payment",
                columns: new[] { "status", "recorded_on" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocation_entry_invoice",
                schema: "billing",
                table: "payment_allocation_entry",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_entry_payment_id",
                schema: "billing",
                table: "payment_allocation_entry",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requested_allocation_payment_id",
                schema: "billing",
                table: "payment_requested_allocation",
                column: "payment_id");

            // US-BIL-11 AC3 / ADR-180: a payment's amount, method, reference, received date, collector and recorder never change
            // (reverse it and record it again) and no payment row is ever deleted; an allocation entry is append-only.
            // Hand-written SQL, read before commit.
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.PaymentImmutability());
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.AppendOnly("payment_allocation_entry"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropAppendOnly("payment_allocation_entry"));
            migrationBuilder.Sql(CatEcommerce.Infrastructure.Billing.BillingMigrationSql.DropPaymentImmutability());

            migrationBuilder.DropTable(
                name: "payment_allocation_entry",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "payment_requested_allocation",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "payment",
                schema: "billing");
        }
    }
}
