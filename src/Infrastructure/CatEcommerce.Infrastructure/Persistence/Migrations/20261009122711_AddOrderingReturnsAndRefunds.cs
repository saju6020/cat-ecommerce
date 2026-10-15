using System;
using CatEcommerce.Infrastructure.Ordering;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderingReturnsAndRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_refund",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    method = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    credit_note_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_order_refund", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "return_request",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    received_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    closed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_return_request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "return_line",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<int>(type: "integer", nullable: false),
                    condition = table.Column<int>(type: "integer", nullable: false),
                    customer_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requires_defect_confirmation = table.Column<bool>(type: "boolean", nullable: false),
                    defect_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    defect_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    quantity_received = table.Column<int>(type: "integer", nullable: false),
                    receipts = table.Column<string>(type: "jsonb", nullable: false),
                    stock_decision = table.Column<int>(type: "integer", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    return_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_line_return_request_return_request_id",
                        column: x => x.return_request_id,
                        principalSchema: "ordering",
                        principalTable: "return_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_refund_order",
                schema: "ordering",
                table: "order_refund",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_return_line_return",
                schema: "ordering",
                table: "return_line",
                column: "return_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_return_request_customer",
                schema: "ordering",
                table: "return_request",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_return_request_order",
                schema: "ordering",
                table: "return_request",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_return_request_status_requested",
                schema: "ordering",
                table: "return_request",
                columns: new[] { "status", "requested_on" });

            // order_refund is append-only (ADR-211, D-5): the database refuses update, delete and truncate.
            migrationBuilder.Sql(OrderingMigrationSql.RefundImmutabilityTriggers());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OrderingMigrationSql.DropRefundImmutabilityTriggers());

            migrationBuilder.DropTable(
                name: "order_refund",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "return_line",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "return_request",
                schema: "ordering");
        }
    }
}
