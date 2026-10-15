using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRecalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recall_closed_on",
                schema: "inventory",
                table: "batch",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recall_closing_note",
                schema: "inventory",
                table: "batch",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recall_customer_advice",
                schema: "inventory",
                table: "batch",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recall_declared_by",
                schema: "inventory",
                table: "batch",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recall_declared_on",
                schema: "inventory",
                table: "batch",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recall_notification_runs",
                schema: "inventory",
                table: "batch",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recall_notified_by",
                schema: "inventory",
                table: "batch",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recall_notified_on",
                schema: "inventory",
                table: "batch",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recall_notified_recipient_count",
                schema: "inventory",
                table: "batch",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recall_number",
                schema: "inventory",
                table: "batch",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recall_reason",
                schema: "inventory",
                table: "batch",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_batch_recall_number",
                schema: "inventory",
                table: "batch",
                column: "recall_number",
                unique: true,
                filter: "recall_number IS NOT NULL");

            // ADR-114 -- RCL-{yyyy}-{seq:D6}; not gapless.
            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS inventory.recall_number_seq START WITH 1 INCREMENT BY 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS inventory.recall_number_seq;");

            migrationBuilder.DropIndex(
                name: "ix_batch_recall_number",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_closed_on",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_closing_note",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_customer_advice",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_declared_by",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_declared_on",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_notification_runs",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_notified_by",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_notified_on",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_notified_recipient_count",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_number",
                schema: "inventory",
                table: "batch");

            migrationBuilder.DropColumn(
                name: "recall_reason",
                schema: "inventory",
                table: "batch");
        }
    }
}
