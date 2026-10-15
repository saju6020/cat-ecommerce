using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderingGateFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "decided_by",
                schema: "ordering",
                table: "return_line");

            migrationBuilder.DropColumn(
                name: "decided_on",
                schema: "ordering",
                table: "return_line");

            migrationBuilder.DropColumn(
                name: "decision_reason",
                schema: "ordering",
                table: "return_line");

            migrationBuilder.DropColumn(
                name: "stock_decision",
                schema: "ordering",
                table: "return_line");

            migrationBuilder.AddColumn<Guid>(
                name: "held_payment_id",
                schema: "ordering",
                table: "sales_order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "ordering",
                table: "reconciliation_issue",
                type: "uuid",
                nullable: true);

            // SECURITY MEDIUM 2: issues raised for an order take that order's branch (a counter-sale issue had no order and stays null -- it is not listed to any branch).
            migrationBuilder.Sql("UPDATE ordering.reconciliation_issue AS i SET branch_id = o.branch_id FROM ordering.sales_order AS o WHERE i.order_id = o.\"Id\" AND i.branch_id IS NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_issue_branch_status_raised",
                schema: "ordering",
                table: "reconciliation_issue",
                columns: new[] { "branch_id", "status", "raised_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reconciliation_issue_branch_status_raised",
                schema: "ordering",
                table: "reconciliation_issue");

            migrationBuilder.DropColumn(
                name: "held_payment_id",
                schema: "ordering",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "ordering",
                table: "reconciliation_issue");

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by",
                schema: "ordering",
                table: "return_line",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "decided_on",
                schema: "ordering",
                table: "return_line",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_reason",
                schema: "ordering",
                table: "return_line",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stock_decision",
                schema: "ordering",
                table: "return_line",
                type: "integer",
                nullable: true);
        }
    }
}
